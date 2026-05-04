using Microsoft.Extensions.Logging;
using MultiTenantETL.Application.Common.Interfaces;
using MultiTenantETL.Application.Connectors.DataReaders;
using MultiTenantETL.Application.Connectors.DataWriters;
using MultiTenantETL.Domain.Entities;
using MultiTenantETL.Infrastructure.Configuration;
using Npgsql;
using System.Text.Json;

namespace MultiTenantETL.Infrastructure.DataWriters;

public class PostgreSqlDataWriter : IDataWriter
{
    private readonly ILogger<PostgreSqlDataWriter> _logger;
    private readonly ISecretResolver _secretResolver;

    public PostgreSqlDataWriter(ILogger<PostgreSqlDataWriter> logger, ISecretResolver secretResolver)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _secretResolver = secretResolver ?? throw new ArgumentNullException(nameof(secretResolver));
    }

    public async Task<DataWriteResult> WriteBatchAsync(
        Connector connector,
        ReadBatch batch,
        WriteOptions options,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var result = new DataWriteResult { BatchId = batch.BatchId };

        var config = ParseConfig(connector.ConfigJson);

        try
        {

            await using var connection = new NpgsqlConnection(config.ConnectionString);
            await connection.OpenAsync(cancellationToken);

            if (options.TruncateBeforeLoad)
            {
                _logger.LogInformation("Truncating table {TableName}", config.TableName);
                await TruncateTableAsync(connection, config.TableName!, cancellationToken);
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
                return await UpsertBatchAsync(connection, config.TableName!, batch, options, cancellationToken);
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
            INSERT INTO ""{tableName}"" ({columnList})
            VALUES ({valuePlaceholders})
            ON CONFLICT ({conflictColumns})
            DO UPDATE SET {updateSet}";

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            for (int rowIndex = 0; rowIndex < batch.Rows.Count; rowIndex++)
            {
                var row = batch.Rows[rowIndex];
                // Use a safe savepoint name (rowIndex is always a non-negative integer from for loop)
                var savepointName = $"sp_row_{rowIndex}";

                try
                {
                    // Create a savepoint before each row operation
                    await using var savepointCommand = new NpgsqlCommand($"SAVEPOINT {savepointName}", connection, transaction);
                    await savepointCommand.ExecuteNonQueryAsync(cancellationToken);

                    await using var command = new NpgsqlCommand(sql, connection, transaction);

                    for (int i = 0; i < columns.Count; i++)
                    {
                        var value = row[columns[i]] ?? DBNull.Value;
                        command.Parameters.AddWithValue($"@p{i}", value);
                    }

                    await command.ExecuteNonQueryAsync(cancellationToken);
                    result.RowsWritten++;

                    // Release the savepoint on success to free resources
                    await using var releaseCommand = new NpgsqlCommand($"RELEASE SAVEPOINT {savepointName}", connection, transaction);
                    await releaseCommand.ExecuteNonQueryAsync(cancellationToken);
                }
                catch (Exception ex)
                {
                    // Rollback to savepoint on error, allowing the transaction to continue
                    try
                    {
                        await using var rollbackCommand = new NpgsqlCommand($"ROLLBACK TO SAVEPOINT {savepointName}", connection, transaction);
                        await rollbackCommand.ExecuteNonQueryAsync(cancellationToken);
                    }
                    catch (Exception rollbackEx)
                    {
                        // If savepoint rollback fails, the transaction is likely in an unrecoverable state
                        // Log the error and let the outer catch block handle transaction rollback
                        _logger.LogError(rollbackEx, "Failed to rollback to savepoint for row {RowIndex}. Transaction may be in invalid state.", rowIndex);
                    }

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
        _logger.LogInformation("Truncating table {TableName}", tableName);
        await using var command = new NpgsqlCommand($"TRUNCATE TABLE {tableName}", connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private PostgreSqlConfig ParseConfig(string configJson)
    {
        PostgreSqlConfig config;
        try
        {
            // Resolve Key Vault secrets
            var resolvedElement = _secretResolver.ResolveSecretsAsync(configJson).GetAwaiter().GetResult();

            config = JsonSerializer.Deserialize<PostgreSqlConfig>(resolvedElement.GetRawText(), JsonSerializerOptionsProvider.Default)
                ?? throw new InvalidOperationException("Invalid PostgreSQL configuration");
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException("Failed to parse PostgreSQL connector configuration", ex);
        }

        // Validate that either ConnectionString is provided, or all required fields for building it
        if (string.IsNullOrEmpty(config.ConnectionString) &&
            (string.IsNullOrEmpty(config.Host) || string.IsNullOrEmpty(config.Database) ||
             string.IsNullOrEmpty(config.Username) || string.IsNullOrEmpty(config.Password)))
        {
            throw new InvalidOperationException("PostgreSQL configuration must include ConnectionString");
        }

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

        if (string.IsNullOrEmpty(config.TableName))
        {
            throw new InvalidOperationException("PostgreSQL configuration must include TableName");
        }

        return config;
    }

    private string BuildConnectionString(PostgreSqlConfig config)
    {
        if (string.IsNullOrEmpty(config.Host))
            throw new InvalidOperationException("PostgreSQL configuration must include Host");

        if (string.IsNullOrEmpty(config.Database))
            throw new InvalidOperationException("PostgreSQL configuration must include Database");

        if (string.IsNullOrEmpty(config.Username))
            throw new InvalidOperationException("PostgreSQL configuration must include Username");

        if (string.IsNullOrEmpty(config.Password))
            throw new InvalidOperationException("PostgreSQL configuration must include Password");

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
