using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MultiTenantETL.Application.Common.Interfaces;
using MultiTenantETL.Application.Connectors.DataReaders;
using MultiTenantETL.Domain.Constants;
using MultiTenantETL.Domain.Entities;
using MultiTenantETL.Infrastructure.Configuration;
using Snowflake.Data.Client;
using IDataReader = MultiTenantETL.Application.Connectors.DataReaders.IDataReader;

namespace MultiTenantETL.Infrastructure.DataReaders;

public class SnowflakeDataReader : IDataReader
{
    private readonly ILogger<SnowflakeDataReader> _logger;
    private readonly EtlSettings _settings;
    private readonly IEncryptionService _encryptionService;

    public SnowflakeDataReader(
        ILogger<SnowflakeDataReader> logger,
        IOptions<EtlSettings> settings,
        IEncryptionService encryptionService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _settings = settings?.Value ?? throw new ArgumentNullException(nameof(settings));
        _encryptionService = encryptionService ?? throw new ArgumentNullException(nameof(encryptionService));
    }

    public async IAsyncEnumerable<ReadBatch> ReadAsync(
        Connector connector,
        ReadOptions options,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var config = ParseConfig(connector.ConfigJson);
        var connectionString = config.ConnectionString;
        var query = config.Query ?? $"SELECT * FROM {config.TableName}";

        await using var connection = new SnowflakeDbConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new SnowflakeDbCommand(connection, query);
        command.CommandTimeout = _settings.CommandTimeoutSeconds;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var batch = new ReadBatch { BatchId = Guid.NewGuid() };
        var rowsRead = 0;

        while (await reader.ReadAsync(cancellationToken))
        {
            var row = new Dictionary<string, object?>();

            for (int i = 0; i < reader.FieldCount; i++)
            {
                var fieldName = reader.GetName(i);
                var value = reader.IsDBNull(i) ? null : reader.GetValue(i);
                row[fieldName] = value;
            }

            batch.Rows.Add(row);
            batch.RowCount++;
            rowsRead++;

            if (batch.RowCount >= options.BatchSize)
            {
                yield return batch;
                batch = new ReadBatch { BatchId = Guid.NewGuid() };
            }

            if (options.MaxRows.HasValue && rowsRead >= options.MaxRows.Value)
            {
                break;
            }
        }

        if (batch.RowCount > 0)
        {
            yield return batch;
        }
    }

    public async Task<bool> TestConnectionAsync(Connector connector, CancellationToken cancellationToken)
    {
        try
        {
            var config = ParseConfig(connector.ConfigJson);
            await using var connection = new SnowflakeDbConnection(config.ConnectionString);
            await connection.OpenAsync(cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Snowflake connection test failed");
            return false;
        }
    }

    public async Task<SchemaDetectionResult> DetectSchemaAsync(Connector connector, CancellationToken cancellationToken)
    {
        try
        {
            var config = ParseConfig(connector.ConfigJson);
            await using var connection = new SnowflakeDbConnection(config.ConnectionString);
            await connection.OpenAsync(cancellationToken);

            var query = $@"
                SELECT
                    COLUMN_NAME,
                    DATA_TYPE,
                    IS_NULLABLE,
                    CHARACTER_MAXIMUM_LENGTH,
                    CASE WHEN COLUMN_NAME IN (
                        SELECT COLUMN_NAME
                        FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
                        JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE kcu
                            ON tc.CONSTRAINT_NAME = kcu.CONSTRAINT_NAME
                        WHERE tc.CONSTRAINT_TYPE = 'PRIMARY KEY'
                            AND tc.TABLE_NAME = '{config.TableName}'
                    ) THEN true ELSE false END AS IS_PRIMARY_KEY
                FROM INFORMATION_SCHEMA.COLUMNS
                WHERE TABLE_NAME = '{config.TableName}'
                ORDER BY ORDINAL_POSITION";

            await using var command = new SnowflakeDbCommand(connection, query);

            var fields = new List<FieldDefinition>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                fields.Add(new FieldDefinition
                {
                    Name = reader.GetString(0),
                    DataType = reader.GetString(1),
                    IsNullable = reader.GetString(2) == "YES",
                    MaxLength = reader.IsDBNull(3) ? null : reader.GetInt32(3),
                    IsPrimaryKey = reader.GetBoolean(4)
                });
            }

            return new SchemaDetectionResult
            {
                Fields = fields,
                Version = 1,
                DetectedAt = DateTimeOffset.UtcNow,
                Success = true
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Schema detection failed for Snowflake");
            return new SchemaDetectionResult
            {
                Success = false,
                ErrorMessage = ex.Message,
                DetectedAt = DateTimeOffset.UtcNow
            };
        }
    }

    private SnowflakeConfig ParseConfig(string configJson)
    {
        var jsonElement = JsonSerializer.Deserialize<JsonElement>(configJson);

        // Decrypt sensitive fields
        var decryptedElement = _encryptionService.DecryptJsonFields(jsonElement, EncryptionConstants.SensitiveFields);

        var config = JsonSerializer.Deserialize<SnowflakeConfig>(decryptedElement.GetRawText(), JsonSerializerOptionsProvider.Default)
            ?? throw new InvalidOperationException("Invalid Snowflake configuration");

        // Build connection string if not provided directly
        if (string.IsNullOrEmpty(config.ConnectionString))
        {
            config.ConnectionString = BuildConnectionString(config);
        }

        return config;
    }

    private string BuildConnectionString(SnowflakeConfig config)
    {
        if (string.IsNullOrEmpty(config.ConnectionString))
        {
            // Build connection string manually for Snowflake
            var parts = new List<string>();
            if (!string.IsNullOrEmpty(config.Account)) parts.Add($"account={config.Account}");
            if (!string.IsNullOrEmpty(config.Username)) parts.Add($"user={config.Username}");
            if (!string.IsNullOrEmpty(config.Password)) parts.Add($"password={config.Password}");
            if (!string.IsNullOrEmpty(config.Database)) parts.Add($"db={config.Database}");
            if (!string.IsNullOrEmpty(config.Schema)) parts.Add($"schema={config.Schema}");
            if (!string.IsNullOrEmpty(config.Warehouse)) parts.Add($"warehouse={config.Warehouse}");
            if (!string.IsNullOrEmpty(config.Role)) parts.Add($"role={config.Role}");
            config.ConnectionString = string.Join(";", parts);
        }
        return config.ConnectionString;
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
        public string? Query { get; set; }
    }
}