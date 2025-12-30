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

/// <summary>
/// Writes data to AWS Redshift using the Npgsql driver.
/// Note: Redshift does NOT support Npgsql's Binary Import (COPY FROM STDIN).
/// This implementation uses multi-row INSERTs for bulk operations.
/// </summary>
public class RedshiftDataWriter : IDataWriter
{
    private readonly ILogger<RedshiftDataWriter> _logger;
    private readonly IEncryptionService _encryptionService;

    public RedshiftDataWriter(ILogger<RedshiftDataWriter> logger, IEncryptionService encryptionService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _encryptionService = encryptionService ?? throw new ArgumentNullException(nameof(encryptionService));
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
                _logger.LogInformation("Truncating Redshift table {TableName}", config.TableName);
                await TruncateTableAsync(connection, config.TableName!, cancellationToken);
            }

            if (batch.Rows.Count == 0)
            {
                return result;
            }

            // Redshift best practice for small-to-medium batches via SQL is multi-row inserts.
            // For massive data, S3 COPY is preferred, but this provides a direct writer.
            return await InsertBatchAsync(connection, config.TableName!, batch, options, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write batch to Redshift");
            result.RowsFailed = batch.RowCount;
            result.Errors.Add(ex.Message);
        }

        return result;
    }

    private async Task<DataWriteResult> InsertBatchAsync(
        NpgsqlConnection connection,
        string tableName,
        ReadBatch batch,
        WriteOptions options,
        CancellationToken cancellationToken)
    {
        var result = new DataWriteResult { BatchId = batch.BatchId };
        var columns = batch.Rows[0].Keys.ToList();
        
        // Build INSERT INTO ... VALUES (...), (...), ...
        var columnList = string.Join(", ", columns.Select(c => $"\"{c}\""));
        
        // We process in smaller sub-batches to avoid hitting SQL length limits
        const int SubBatchSize = 100; 
        
        for (int i = 0; i < batch.Rows.Count; i += SubBatchSize)
        {
            var subBatch = batch.Rows.Skip(i).Take(SubBatchSize).ToList();
            var valuesPlaceholders = new List<string>();
            var parameters = new List<NpgsqlParameter>();
            
            for (int r = 0; r < subBatch.Count; r++)
            {
                var rowPlaceholders = new List<string>();
                for (int c = 0; c < columns.Count; c++)
                {
                    var paramName = $"p_{i}_{r}_{c}";
                    rowPlaceholders.Add($"@{paramName}");
                    parameters.Add(new NpgsqlParameter(paramName, subBatch[r][columns[c]] ?? DBNull.Value));
                }
                valuesPlaceholders.Add($"({string.Join(", ", rowPlaceholders)})");
            }

            var sql = $"INSERT INTO \"{tableName}\" ({columnList}) VALUES {string.Join(", ", valuesPlaceholders)}";
            
            await using var command = new NpgsqlCommand(sql, connection);
            command.Parameters.AddRange(parameters.ToArray());
            
            var affected = await command.ExecuteNonQueryAsync(cancellationToken);
            result.RowsWritten += subBatch.Count;
        }

        return result;
    }

    private async Task TruncateTableAsync(NpgsqlConnection connection, string tableName, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand($"TRUNCATE TABLE \"{tableName}\"", connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private RedshiftConfig ParseConfig(string configJson)
    {
        var jsonElement = JsonSerializer.Deserialize<JsonElement>(configJson);
        var decryptedElement = _encryptionService.DecryptJsonFields(jsonElement, EncryptionConstants.SensitiveFields);
        
        var config = JsonSerializer.Deserialize<RedshiftConfig>(decryptedElement.GetRawText(), JsonSerializerOptionsProvider.Default)
            ?? throw new InvalidOperationException("Invalid Redshift configuration");

        if (string.IsNullOrEmpty(config.ConnectionString))
        {
            config.ConnectionString = BuildConnectionString(config);
        }

        if (string.IsNullOrEmpty(config.TableName) && config.WriteConfig != null)
        {
            config.TableName = config.WriteConfig.TableName;
        }

        if (string.IsNullOrEmpty(config.TableName))
        {
            throw new InvalidOperationException("Redshift configuration must include TableName");
        }

        return config;
    }

    private string BuildConnectionString(RedshiftConfig config)
    {
        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = config.Host,
            Port = config.Port > 0 ? config.Port : 5439,
            Database = config.Database,
            Username = config.Username ?? string.Empty,
            Password = config.Password ?? string.Empty,
            SslMode = SslMode.Require
        };

        return builder.ToString();
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }

    private class RedshiftConfig
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
    }
}
