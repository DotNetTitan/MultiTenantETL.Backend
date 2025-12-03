using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using MultiTenantETL.Application.Common.Interfaces;
using MultiTenantETL.Application.Connectors.DataReaders;
using MultiTenantETL.Application.Connectors.DataWriters;
using MultiTenantETL.Domain.Constants;
using MultiTenantETL.Domain.Entities;
using MultiTenantETL.Infrastructure.Configuration;
using Npgsql;

namespace MultiTenantETL.Infrastructure.DataWriters;

public class PostgreSqlDataWriter : IDataWriter
{
    private readonly ILogger<PostgreSqlDataWriter> _logger;
    private readonly IEncryptionService _encryptionService;

    public PostgreSqlDataWriter(ILogger<PostgreSqlDataWriter> logger, IEncryptionService encryptionService)
    {
        _logger = logger;
        _encryptionService = encryptionService;
    }

    public async Task<DataWriteResult> WriteBatchAsync(
        Connector connector, 
        ReadBatch batch, 
        WriteOptions options,
        CancellationToken cancellationToken)
    {
        var result = new DataWriteResult { BatchId = batch.BatchId };
        
        try
        {
            var config = ParseConfig(connector.ConfigJson);
            await using var connection = new NpgsqlConnection(config.ConnectionString);
            await connection.OpenAsync(cancellationToken);

            if (options.TruncateBeforeLoad)
            {
                await TruncateTableAsync(connection, config.TableName, cancellationToken);
            }

            _logger.LogDebug("Batch has {RowCount} rows", batch.Rows.Count);
            
            if (batch.Rows.Count == 0)
            {
                _logger.LogWarning("Batch is empty, nothing to write");
                return result;
            }

            // Use upsert if requested and keys are provided
            if (options.UseUpsert && options.UpsertKeys?.Count > 0)
            {
                return await UpsertBatchAsync(connection, config.TableName, batch, options, cancellationToken);
            }

            // Use COPY for bulk insert (fastest for PostgreSQL)
            var columns = batch.Rows[0].Keys.ToList();
            _logger.LogDebug("First row has {ColumnCount} columns: {Columns}", columns.Count, string.Join(", ", columns));
            
            if (columns.Count == 0)
            {
                _logger.LogError("First row has no columns! Batch RowCount: {RowCount}", batch.RowCount);
                result.RowsFailed = batch.RowCount;
                result.Errors.Add("Batch rows have no columns after field mapping");
                return result;
            }
            
            var quotedColumns = columns.Select(c => $"\"{c}\"");
            var copyCommand = $"COPY \"{config.TableName}\" ({string.Join(", ", quotedColumns)}) FROM STDIN (FORMAT BINARY)";
            
            _logger.LogDebug("COPY command: {CopyCommand}", copyCommand);

            await using var writer = await connection.BeginBinaryImportAsync(copyCommand, cancellationToken);

            foreach (var row in batch.Rows)
            {
                await writer.StartRowAsync(cancellationToken);
                
                foreach (var column in columns)
                {
                    var value = row[column];
                    if (value == null)
                    {
                        await writer.WriteNullAsync(cancellationToken);
                    }
                    else
                    {
                        await writer.WriteAsync(value, cancellationToken);
                    }
                }
            }

            await writer.CompleteAsync(cancellationToken);
            
            result.RowsWritten = batch.RowCount;
            result.RowsFailed = 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write batch to PostgreSQL");
            result.RowsFailed = batch.RowCount;
            result.Errors.Add(ex.Message);
        }

        return result;
    }

    private async Task<DataWriteResult> UpsertBatchAsync(
        NpgsqlConnection connection,
        string tableName,
        ReadBatch batch,
        WriteOptions options,
        CancellationToken cancellationToken)
    {
        var result = new DataWriteResult { BatchId = batch.BatchId };
        var columns = batch.Rows[0].Keys.ToList();
        var upsertKeys = options.UpsertKeys!;

        // Build INSERT ... ON CONFLICT ... DO UPDATE statement
        var columnList = string.Join(", ", columns.Select(c => $"\"{c}\""));
        var valuePlaceholders = string.Join(", ", columns.Select((_, i) => $"@p{i}"));
        var conflictColumns = string.Join(", ", upsertKeys.Select(k => $"\"{k}\""));
        
        // Columns to update (exclude upsert keys)
        var updateColumns = columns.Except(upsertKeys).ToList();
        var updateSet = string.Join(", ", updateColumns.Select(c => $"\"{c}\" = EXCLUDED.\"{c}\""));

        var sql = $@"
            INSERT INTO {tableName} ({columnList})
            VALUES ({valuePlaceholders})
            ON CONFLICT ({conflictColumns})
            DO UPDATE SET {updateSet}";

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            for (int rowIndex = 0; rowIndex < batch.Rows.Count; rowIndex++)
            {
                var row = batch.Rows[rowIndex];
                
                try
                {
                    await using var command = new NpgsqlCommand(sql, connection, transaction);
                    
                    for (int i = 0; i < columns.Count; i++)
                    {
                        var value = row[columns[i]] ?? DBNull.Value;
                        command.Parameters.AddWithValue($"@p{i}", value);
                    }

                    await command.ExecuteNonQueryAsync(cancellationToken);
                    result.RowsWritten++;
                }
                catch (Exception ex)
                {
                    result.RowsFailed++;
                    result.RowErrors.Add(new RowError
                    {
                        RowIndex = rowIndex,
                        ErrorMessage = ex.Message,
                        ErrorCode = ex is PostgresException pgEx ? pgEx.SqlState : null,
                        RowData = row
                    });
                    
                    _logger.LogWarning(ex, "Failed to upsert row {RowIndex} in batch {BatchId}", rowIndex, batch.BatchId);
                }
            }

            await transaction.CommitAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "Transaction failed for batch {BatchId}", batch.BatchId);
            result.Errors.Add($"Transaction failed: {ex.Message}");
        }

        return result;
    }

    private async Task TruncateTableAsync(NpgsqlConnection connection, string tableName, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand($"TRUNCATE TABLE {tableName}", connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private PostgreSqlConfig ParseConfig(string configJson)
    {
        var jsonElement = JsonSerializer.Deserialize<JsonElement>(configJson);
        
        // Decrypt sensitive fields
        var decryptedElement = _encryptionService.DecryptJsonFields(jsonElement, EncryptionConstants.SensitiveFields);
        
        var config = JsonSerializer.Deserialize<PostgreSqlConfig>(decryptedElement.GetRawText(), JsonSerializerOptionsProvider.Default)
            ?? throw new InvalidOperationException("Invalid PostgreSQL configuration");

        // Build connection string if not provided directly
        if (string.IsNullOrEmpty(config.ConnectionString))
        {
            config.ConnectionString = BuildConnectionString(config);
        }

        // For destination connectors, table name might be in writeConfig
        if (string.IsNullOrEmpty(config.TableName) && config.WriteConfig != null)
        {
            config.TableName = config.WriteConfig.TableName;
        }

        return config;
    }

    private string BuildConnectionString(PostgreSqlConfig config)
    {
        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = config.Host,
            Port = config.Port > 0 ? config.Port : 5432,
            Database = config.Database,
            Username = config.Username ?? string.Empty,
            Password = config.Password ?? string.Empty
        };

        return builder.ToString();
    }

    public ValueTask DisposeAsync()
    {
        // No resources to dispose for database writer
        return ValueTask.CompletedTask;
    }

    private class PostgreSqlConfig
    {
        public string? ConnectionString { get; set; }
        public string? Host { get; set; }
        public int Port { get; set; }
        public string? Database { get; set; }
        public string? Username { get; set; }
        public string? Password { get; set; }
        public string? TableName { get; set; }
        public WriteConfigSection? WriteConfig { get; set; }
    }

    private class WriteConfigSection
    {
        public string? TableName { get; set; }
        public string? Operation { get; set; }
        public List<string>? PrimaryKeys { get; set; }
        public int BatchSize { get; set; }
    }
}
