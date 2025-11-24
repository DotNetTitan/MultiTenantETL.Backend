using System.Data;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using MultiTenantETL.Application.Connectors;
using MultiTenantETL.Application.Connectors.Models;
using MultiTenantETL.Domain.Constants;
using Npgsql;
using MySqlConnector;

namespace MultiTenantETL.Infrastructure.Services;

public class SchemaDetector : ISchemaDetector
{
    private readonly ILogger<SchemaDetector> _logger;

    public SchemaDetector(ILogger<SchemaDetector> logger)
    {
        _logger = logger;
    }

    public async Task<SchemaDetectionResult> DetectSchemaAsync(
        string type,
        string provider,
        JsonElement config,
        string? tableOrResourceName)
    {
        try
        {
            return type switch
            {
                ConnectorTypes.Database => await DetectDatabaseSchemaAsync(provider, config, tableOrResourceName),
                ConnectorTypes.File => DetectFileSchemaAsync(provider, config),
                ConnectorTypes.Api => new SchemaDetectionResult
                {
                    Success = false,
                    Message = "Schema detection for API connectors requires manual definition"
                },
                _ => new SchemaDetectionResult
                {
                    Success = false,
                    Message = $"Unsupported connector type: {type}"
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Schema detection failed for type {Type}, provider {Provider}", type, provider);
            return new SchemaDetectionResult
            {
                Success = false,
                Message = $"Schema detection failed: {ex.Message}"
            };
        }
    }

    private async Task<SchemaDetectionResult> DetectDatabaseSchemaAsync(
        string provider,
        JsonElement config,
        string? tableName)
    {
        if (string.IsNullOrWhiteSpace(tableName))
        {
            return new SchemaDetectionResult
            {
                Success = false,
                Message = "Table name is required for database schema detection"
            };
        }

        var dbConfig = JsonSerializer.Deserialize<DatabaseConfig>(config);
        if (dbConfig == null)
        {
            return new SchemaDetectionResult
            {
                Success = false,
                Message = "Invalid database configuration"
            };
        }

        try
        {
            var fields = provider switch
            {
                ConnectorProviders.SqlServer => await DetectSqlServerSchemaAsync(dbConfig, tableName),
                ConnectorProviders.PostgreSQL => await DetectPostgreSqlSchemaAsync(dbConfig, tableName),
                ConnectorProviders.MySQL => await DetectMySqlSchemaAsync(dbConfig, tableName),
                _ => throw new NotSupportedException($"Database provider {provider} is not supported")
            };

            if (fields.Count == 0)
            {
                return new SchemaDetectionResult
                {
                    Success = false,
                    Message = $"Table '{tableName}' not found or has no columns"
                };
            }

            var schema = new
            {
                TableName = tableName,
                Fields = fields,
                DetectedAt = DateTime.UtcNow,
                Provider = provider
            };

            return new SchemaDetectionResult
            {
                Success = true,
                Message = $"Successfully detected schema for table '{tableName}' with {fields.Count} fields",
                Schema = JsonSerializer.SerializeToElement(schema)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Schema detection failed for table {TableName}", tableName);
            return new SchemaDetectionResult
            {
                Success = false,
                Message = $"Schema detection failed: {ex.Message}"
            };
        }
    }

    private async Task<List<SchemaField>> DetectSqlServerSchemaAsync(DatabaseConfig config, string tableName)
    {
        using var connection = new SqlConnection(BuildSqlServerConnectionString(config));
        await connection.OpenAsync();

        var query = @"
            SELECT 
                c.COLUMN_NAME as Name,
                c.DATA_TYPE as DataType,
                c.IS_NULLABLE as IsNullable,
                c.CHARACTER_MAXIMUM_LENGTH as MaxLength,
                c.NUMERIC_PRECISION as Precision,
                c.NUMERIC_SCALE as Scale,
                c.COLUMN_DEFAULT as DefaultValue,
                CASE WHEN pk.COLUMN_NAME IS NOT NULL THEN 1 ELSE 0 END as IsPrimaryKey
            FROM INFORMATION_SCHEMA.COLUMNS c
            LEFT JOIN (
                SELECT ku.COLUMN_NAME
                FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
                JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE ku
                    ON tc.CONSTRAINT_NAME = ku.CONSTRAINT_NAME
                WHERE tc.CONSTRAINT_TYPE = 'PRIMARY KEY'
                    AND ku.TABLE_NAME = @TableName
            ) pk ON c.COLUMN_NAME = pk.COLUMN_NAME
            WHERE c.TABLE_NAME = @TableName
            ORDER BY c.ORDINAL_POSITION";

        using var command = connection.CreateCommand();
        command.CommandText = query;
        var param = command.CreateParameter();
        param.ParameterName = "@TableName";
        param.Value = tableName;
        command.Parameters.Add(param);

        var fields = new List<SchemaField>();
        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            fields.Add(new SchemaField
            {
                Name = reader.GetString(0),
                DataType = reader.GetString(1),
                IsNullable = reader.GetString(2) == "YES",
                IsPrimaryKey = reader.GetInt32(7) == 1,
                MaxLength = reader.IsDBNull(3) ? null : reader.GetInt32(3),
                Precision = reader.IsDBNull(4) ? null : Convert.ToInt32(reader.GetByte(4)),
                Scale = reader.IsDBNull(5) ? null : reader.GetInt32(5),
                DefaultValue = reader.IsDBNull(6) ? null : reader.GetString(6)
            });
        }

        return fields;
    }

    private async Task<List<SchemaField>> DetectPostgreSqlSchemaAsync(DatabaseConfig config, string tableName)
    {
        using var connection = new NpgsqlConnection(BuildPostgreSqlConnectionString(config));
        await connection.OpenAsync();

        var query = @"
            SELECT 
                c.column_name as Name,
                c.data_type as DataType,
                c.is_nullable as IsNullable,
                c.character_maximum_length as MaxLength,
                c.numeric_precision as Precision,
                c.numeric_scale as Scale,
                c.column_default as DefaultValue,
                CASE WHEN pk.column_name IS NOT NULL THEN true ELSE false END as IsPrimaryKey
            FROM information_schema.columns c
            LEFT JOIN (
                SELECT ku.column_name
                FROM information_schema.table_constraints tc
                JOIN information_schema.key_column_usage ku
                    ON tc.constraint_name = ku.constraint_name
                WHERE tc.constraint_type = 'PRIMARY KEY'
                    AND ku.table_name = @TableName
            ) pk ON c.column_name = pk.column_name
            WHERE c.table_name = @TableName
            ORDER BY c.ordinal_position";

        using var command = connection.CreateCommand();
        command.CommandText = query;
        var param = command.CreateParameter();
        param.ParameterName = "@TableName";
        param.Value = tableName;
        command.Parameters.Add(param);

        var fields = new List<SchemaField>();
        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            fields.Add(new SchemaField
            {
                Name = reader.GetString(0),
                DataType = reader.GetString(1),
                IsNullable = reader.GetString(2) == "YES",
                IsPrimaryKey = reader.GetBoolean(7),
                MaxLength = reader.IsDBNull(3) ? null : reader.GetInt32(3),
                Precision = reader.IsDBNull(4) ? null : reader.GetInt32(4),
                Scale = reader.IsDBNull(5) ? null : reader.GetInt32(5),
                DefaultValue = reader.IsDBNull(6) ? null : reader.GetString(6)
            });
        }

        return fields;
    }

    private async Task<List<SchemaField>> DetectMySqlSchemaAsync(DatabaseConfig config, string tableName)
    {
        using var connection = new MySqlConnection(BuildMySqlConnectionString(config));
        await connection.OpenAsync();

        var query = @"
            SELECT 
                c.COLUMN_NAME as Name,
                c.DATA_TYPE as DataType,
                c.IS_NULLABLE as IsNullable,
                c.CHARACTER_MAXIMUM_LENGTH as MaxLength,
                c.NUMERIC_PRECISION as Precision,
                c.NUMERIC_SCALE as Scale,
                c.COLUMN_DEFAULT as DefaultValue,
                CASE WHEN c.COLUMN_KEY = 'PRI' THEN 1 ELSE 0 END as IsPrimaryKey
            FROM INFORMATION_SCHEMA.COLUMNS c
            WHERE c.TABLE_NAME = @TableName
                AND c.TABLE_SCHEMA = DATABASE()
            ORDER BY c.ORDINAL_POSITION";

        using var command = connection.CreateCommand();
        command.CommandText = query;
        var param = command.CreateParameter();
        param.ParameterName = "@TableName";
        param.Value = tableName;
        command.Parameters.Add(param);

        var fields = new List<SchemaField>();
        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            fields.Add(new SchemaField
            {
                Name = reader.GetString(0),
                DataType = reader.GetString(1),
                IsNullable = reader.GetString(2) == "YES",
                IsPrimaryKey = reader.GetInt32(7) == 1,
                MaxLength = reader.IsDBNull(3) ? null : Convert.ToInt32(reader.GetInt64(3)),
                Precision = reader.IsDBNull(4) ? null : Convert.ToInt32(reader.GetUInt64(4)),
                Scale = reader.IsDBNull(5) ? null : Convert.ToInt32(reader.GetUInt64(5)),
                DefaultValue = reader.IsDBNull(6) ? null : reader.GetString(6)
            });
        }

        return fields;
    }

    private SchemaDetectionResult DetectFileSchemaAsync(string provider, JsonElement config)
    {
        // For files, schema detection would require reading the file
        // This is a simplified version - full implementation would parse CSV headers, Excel sheets, JSON structure
        return new SchemaDetectionResult
        {
            Success = false,
            Message = "File schema detection requires file upload and parsing. Please define schema manually."
        };
    }

    // Connection string builders (same as ConnectionTester)
    private static string BuildSqlServerConnectionString(DatabaseConfig config)
    {
        if (!string.IsNullOrEmpty(config.ConnectionString))
        {
            return config.ConnectionString;
        }

        var builder = new SqlConnectionStringBuilder
        {
            DataSource = $"{config.Host},{config.Port}",
            InitialCatalog = config.Database,
            UserID = config.Username,
            Password = config.Password,
            TrustServerCertificate = true
        };

        return builder.ConnectionString;
    }

    private static string BuildPostgreSqlConnectionString(DatabaseConfig config)
    {
        if (!string.IsNullOrEmpty(config.ConnectionString))
        {
            return config.ConnectionString;
        }

        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = config.Host,
            Port = config.Port,
            Database = config.Database,
            Username = config.Username,
            Password = config.Password,
            SslMode = config.UseSsl ? SslMode.Require : SslMode.Prefer
        };

        return builder.ConnectionString;
    }

    private static string BuildMySqlConnectionString(DatabaseConfig config)
    {
        if (!string.IsNullOrEmpty(config.ConnectionString))
        {
            return config.ConnectionString;
        }

        var builder = new MySqlConnectionStringBuilder
        {
            Server = config.Host,
            Port = (uint)config.Port,
            Database = config.Database,
            UserID = config.Username,
            Password = config.Password
        };

        return builder.ConnectionString;
    }
}
