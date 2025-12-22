using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using MultiTenantETL.Application.Common.Interfaces;
using MultiTenantETL.Application.Connectors.DataReaders;
using MultiTenantETL.Application.Connectors.DataWriters;
using MultiTenantETL.Domain.Constants;
using MultiTenantETL.Domain.Entities;
using MultiTenantETL.Infrastructure.Configuration;
using Snowflake.Data.Client;

namespace MultiTenantETL.Infrastructure.DataWriters;

public class SnowflakeDataWriter : IDataWriter
{
    private readonly ILogger<SnowflakeDataWriter> _logger;
    private readonly IEncryptionService _encryptionService;

    public SnowflakeDataWriter(ILogger<SnowflakeDataWriter> logger, IEncryptionService encryptionService)
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
            await using var connection = new SnowflakeDbConnection(config.ConnectionString);
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

            // Use bulk insert for Snowflake
            var columns = batch.Rows[0].Keys.ToList();
            _logger.LogDebug("First row has {ColumnCount} columns: {Columns}", columns.Count, string.Join(", ", columns));

            if (columns.Count == 0)
            {
                _logger.LogError("First row has no columns! Batch RowCount: {RowCount}", batch.RowCount);
                result.RowsFailed = batch.RowCount;
                result.Errors.Add("Batch rows have no columns after field mapping");
                return result;
            }

            // Build INSERT statement
            var quotedColumns = columns.Select(c => $"\"{c}\"");
            var placeholders = string.Join(", ", columns.Select((_, i) => $"@p{i}"));
            var insertCommand = $"INSERT INTO \"{config.TableName}\" ({string.Join(", ", quotedColumns)}) VALUES ({placeholders})";

            _logger.LogDebug("INSERT command: {InsertCommand}", insertCommand);

            foreach (var row in batch.Rows)
            {
                await using var command = new SnowflakeDbCommand(connection, insertCommand);

                for (int i = 0; i < columns.Count; i++)
                {
                    var value = row[columns[i]];
                    var param = command.CreateParameter();
                    param.ParameterName = $"@p{i}";
                    param.Value = value ?? DBNull.Value;
                    command.Parameters.Add(param);
                }

                try
                {
                    await command.ExecuteNonQueryAsync(cancellationToken);
                    result.RowsWritten++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to insert row");
                    result.RowsFailed++;
                    result.Errors.Add($"Row insert failed: {ex.Message}");
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Batch write failed for Snowflake");
            result.RowsFailed = batch.RowCount;
            result.Errors.Add($"Batch write failed: {ex.Message}");
            return result;
        }
    }

    private async Task TruncateTableAsync(SnowflakeDbConnection connection, string tableName, CancellationToken cancellationToken)
    {
        var truncateCommand = $"TRUNCATE TABLE \"{tableName}\"";
        await using var command = new SnowflakeDbCommand(connection, truncateCommand);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task<DataWriteResult> UpsertBatchAsync(
        SnowflakeDbConnection connection,
        string tableName,
        ReadBatch batch,
        WriteOptions options,
        CancellationToken cancellationToken)
    {
        var result = new DataWriteResult { BatchId = batch.BatchId };

        if (batch.Rows.Count == 0)
            return result;

        var columns = batch.Rows[0].Keys.ToList();
        var upsertKeys = options.UpsertKeys!;

        // Build MERGE statement for upsert
        var quotedColumns = columns.Select(c => $"\"{c}\"");
        var nonKeyColumns = columns.Except(upsertKeys).ToList();

        var mergeSql = new StringBuilder();
        mergeSql.AppendLine($"MERGE INTO \"{tableName}\" AS target");
        mergeSql.AppendLine("USING (SELECT ");
        mergeSql.AppendLine(string.Join(", ", columns.Select((c, i) => $"@p{i} AS \"{c}\"")));
        mergeSql.AppendLine(") AS source");
        mergeSql.AppendLine("ON " + string.Join(" AND ", upsertKeys.Select(k => $"target.\"{k}\" = source.\"{k}\"")));
        mergeSql.AppendLine("WHEN MATCHED THEN UPDATE SET");
        mergeSql.AppendLine(string.Join(", ", nonKeyColumns.Select(c => $"target.\"{c}\" = source.\"{c}\"")));
        mergeSql.AppendLine("WHEN NOT MATCHED THEN INSERT (");
        mergeSql.AppendLine(string.Join(", ", quotedColumns));
        mergeSql.AppendLine(") VALUES (");
        mergeSql.AppendLine(string.Join(", ", columns.Select(c => $"source.\"{c}\"")));
        mergeSql.AppendLine(");");

        foreach (var row in batch.Rows)
        {
            await using var command = new SnowflakeDbCommand(connection, mergeSql.ToString());

            for (int i = 0; i < columns.Count; i++)
            {
                var value = row[columns[i]];
                var param = command.CreateParameter();
                param.ParameterName = $"@p{i}";
                param.Value = value ?? DBNull.Value;
                command.Parameters.Add(param);
            }

            try
            {
                await command.ExecuteNonQueryAsync(cancellationToken);
                result.RowsWritten++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to upsert row");
                result.RowsFailed++;
                result.Errors.Add($"Row upsert failed: {ex.Message}");
            }
        }

        return result;
    }

    private SnowflakeConfig ParseConfig(string configJson)
    {
        SnowflakeConfig config;

        try
        {
            var jsonElement = JsonSerializer.Deserialize<JsonElement>(configJson);

            // Decrypt sensitive fields
            var decryptedElement = _encryptionService.DecryptJsonFields(jsonElement, EncryptionConstants.SensitiveFields);

            config = JsonSerializer.Deserialize<SnowflakeConfig>(decryptedElement.GetRawText(), JsonSerializerOptionsProvider.Default)
                ?? throw new InvalidOperationException("Invalid Snowflake configuration");
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException("Failed to parse Snowflake connector configuration", ex);
        }

        // Validate that either ConnectionString is provided, or all required fields for building it
        if (string.IsNullOrEmpty(config.ConnectionString) &&
            (string.IsNullOrEmpty(config.Account) || string.IsNullOrEmpty(config.Database) ||
             string.IsNullOrEmpty(config.Username) || string.IsNullOrEmpty(config.Password)))
        {
            throw new InvalidOperationException("Snowflake configuration must include ConnectionString or Account/Database/Username/Password");
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
            throw new InvalidOperationException("Snowflake configuration must include TableName");
        }

        return config;
    }

    private string BuildConnectionString(SnowflakeConfig config)
    {
        if (!string.IsNullOrEmpty(config.ConnectionString))
        {
            return config.ConnectionString;
        }

        // Build connection string manually for Snowflake
        var parts = new List<string>();
        if (!string.IsNullOrEmpty(config.Account)) parts.Add($"account={config.Account}");
        if (!string.IsNullOrEmpty(config.Username)) parts.Add($"user={config.Username}");
        if (!string.IsNullOrEmpty(config.Password)) parts.Add($"password={config.Password}");
        if (!string.IsNullOrEmpty(config.Database)) parts.Add($"db={config.Database}");
        if (!string.IsNullOrEmpty(config.Schema)) parts.Add($"schema={config.Schema}");
        if (!string.IsNullOrEmpty(config.Warehouse)) parts.Add($"warehouse={config.Warehouse}");
        if (!string.IsNullOrEmpty(config.Role)) parts.Add($"role={config.Role}");
        return string.Join(";", parts);
    }

    public ValueTask DisposeAsync()
    {
        // No resources to dispose for database writer
        return ValueTask.CompletedTask;
    }

    private class SnowflakeConfig
    {
        public string? ConnectionString { get; set; }
        public string? Account { get; set; }
        public string? Username { get; set; }
        public string? Password { get; set; }
        public string? Database { get; set; }
        public string? Schema { get; set; }
        public string? Warehouse { get; set; }
        public string? Role { get; set; }
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