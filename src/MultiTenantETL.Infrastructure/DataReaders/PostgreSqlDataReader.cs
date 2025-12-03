using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MultiTenantETL.Application.Common.Interfaces;
using MultiTenantETL.Application.Connectors.DataReaders;
using MultiTenantETL.Domain.Constants;
using MultiTenantETL.Domain.Entities;
using MultiTenantETL.Infrastructure.Configuration;
using Npgsql;
using IDataReader = MultiTenantETL.Application.Connectors.DataReaders.IDataReader;

namespace MultiTenantETL.Infrastructure.DataReaders;

public class PostgreSqlDataReader : IDataReader
{
    private readonly ILogger<PostgreSqlDataReader> _logger;
    private readonly EtlSettings _settings;
    private readonly IEncryptionService _encryptionService;

    public PostgreSqlDataReader(
        ILogger<PostgreSqlDataReader> logger, 
        IOptions<EtlSettings> settings,
        IEncryptionService encryptionService)
    {
        _logger = logger;
        _settings = settings.Value;
        _encryptionService = encryptionService;
    }

    public async IAsyncEnumerable<ReadBatch> ReadAsync(
        Connector connector, 
        ReadOptions options, 
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var config = ParseConfig(connector.ConfigJson);
        var connectionString = config.ConnectionString;
        var query = config.Query ?? $"SELECT * FROM {config.TableName}";

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new NpgsqlCommand(query, connection);
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
            await using var connection = new NpgsqlConnection(config.ConnectionString);
            await connection.OpenAsync(cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PostgreSQL connection test failed");
            return false;
        }
    }

    public async Task<SchemaDetectionResult> DetectSchemaAsync(Connector connector, CancellationToken cancellationToken)
    {
        try
        {
            var config = ParseConfig(connector.ConfigJson);
            await using var connection = new NpgsqlConnection(config.ConnectionString);
            await connection.OpenAsync(cancellationToken);

            var query = $@"
                SELECT 
                    c.column_name,
                    c.data_type,
                    c.is_nullable,
                    c.character_maximum_length,
                    CASE WHEN pk.column_name IS NOT NULL THEN true ELSE false END AS is_primary_key
                FROM information_schema.columns c
                LEFT JOIN (
                    SELECT ku.column_name
                    FROM information_schema.table_constraints tc
                    INNER JOIN information_schema.key_column_usage ku
                        ON tc.constraint_name = ku.constraint_name
                    WHERE tc.constraint_type = 'PRIMARY KEY'
                        AND tc.table_name = @TableName
                ) pk ON c.column_name = pk.column_name
                WHERE c.table_name = @TableName
                ORDER BY c.ordinal_position";

            await using var command = new NpgsqlCommand(query, connection);
            command.Parameters.AddWithValue("@TableName", config.TableName ?? string.Empty);

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
            _logger.LogError(ex, "Schema detection failed for PostgreSQL");
            return new SchemaDetectionResult
            {
                Success = false,
                ErrorMessage = ex.Message,
                DetectedAt = DateTimeOffset.UtcNow
            };
        }
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

    private class PostgreSqlConfig
    {
        public string? ConnectionString { get; set; }
        public string? Host { get; set; }
        public int Port { get; set; }
        public string? Database { get; set; }
        public string? Username { get; set; }
        public string? Password { get; set; }
        public string? TableName { get; set; }
        public string? Query { get; set; }
    }
}
