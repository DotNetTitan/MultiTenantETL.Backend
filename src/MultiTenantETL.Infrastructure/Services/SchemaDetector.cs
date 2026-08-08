using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using MultiTenantETL.Application.Connectors;
using MultiTenantETL.Application.Connectors.Models;
using MultiTenantETL.Domain.Constants;
using MultiTenantETL.Infrastructure.Configuration;
using MultiTenantETL.Infrastructure.Security;
using MySqlConnector;
using Npgsql;
using Oracle.ManagedDataAccess.Client;
using System.Text.Json;

namespace MultiTenantETL.Infrastructure.Services;

public class SchemaDetector : ISchemaDetector
{
    private readonly ILogger<SchemaDetector> _logger;
    private readonly ISsrfGuard _ssrfGuard;

    public SchemaDetector(ILogger<SchemaDetector> logger, ISsrfGuard ssrfGuard)
    {
        _logger = logger;
        _ssrfGuard = ssrfGuard;
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
                ConnectorTypes.Api => await DetectApiSchemaAsync(provider, config, tableOrResourceName),
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

        var dbConfig = JsonSerializer.Deserialize<DatabaseConfig>(config, JsonSerializerOptionsProvider.Default);
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
                ConnectorProviders.Oracle => await DetectOracleSchemaAsync(dbConfig, tableName),
                ConnectorProviders.MongoDb => await DetectMongoDbSchemaAsync(dbConfig, tableName),
                ConnectorProviders.CosmosDb => await DetectCosmosDbSchemaAsync(dbConfig, tableName),
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
                DataType = NormalizeDataType(reader.GetString(1)),
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
                DataType = NormalizeDataType(reader.GetString(1)),
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
                DataType = NormalizeDataType(reader.GetString(1)),
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

    private async Task<List<SchemaField>> DetectOracleSchemaAsync(DatabaseConfig config, string tableName)
    {
        using var connection = new OracleConnection(BuildOracleConnectionString(config));
        await connection.OpenAsync();

        var query = @"
            SELECT
                c.COLUMN_NAME as Name,
                c.DATA_TYPE as DataType,
                c.NULLABLE as IsNullable,
                c.DATA_LENGTH as MaxLength,
                c.DATA_PRECISION as Precision,
                c.DATA_SCALE as Scale,
                c.DATA_DEFAULT as DefaultValue,
                CASE WHEN pk.COLUMN_NAME IS NOT NULL THEN 1 ELSE 0 END as IsPrimaryKey
            FROM ALL_TAB_COLUMNS c
            LEFT JOIN (
                SELECT acc.COLUMN_NAME
                FROM ALL_CONSTRAINTS ac
                INNER JOIN ALL_CONS_COLUMNS acc
                    ON ac.CONSTRAINT_TYPE = 'P'
                    AND ac.CONSTRAINT_NAME = acc.CONSTRAINT_NAME
                    AND ac.OWNER = acc.OWNER
                    AND ac.TABLE_NAME = :TableName
            ) pk ON c.COLUMN_NAME = pk.COLUMN_NAME AND c.TABLE_NAME = :TableName
            WHERE c.TABLE_NAME = :TableName
                AND c.OWNER = USER
            ORDER BY c.COLUMN_ID";

        using var command = new OracleCommand(query, connection);
        command.Parameters.Add(new OracleParameter(":TableName", tableName));

        var fields = new List<SchemaField>();
        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            fields.Add(new SchemaField
            {
                Name = reader.GetString(0),
                DataType = NormalizeDataType(reader.GetString(1)),
                IsNullable = reader.GetString(2) == "Y",
                IsPrimaryKey = reader.GetInt32(7) == 1,
                MaxLength = reader.IsDBNull(3) ? null : Convert.ToInt32(reader.GetInt64(3)),
                Precision = reader.IsDBNull(4) ? null : Convert.ToInt32(reader.GetInt64(4)),
                Scale = reader.IsDBNull(5) ? null : Convert.ToInt32(reader.GetInt64(5)),
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

    private async Task<SchemaDetectionResult> DetectApiSchemaAsync(string provider, JsonElement config, string? endpointPath)
    {
        if (string.IsNullOrWhiteSpace(endpointPath))
        {
            return new SchemaDetectionResult
            {
                Success = false,
                Message = "Endpoint path is required for API schema detection"
            };
        }

        var apiConfig = JsonSerializer.Deserialize<ApiConfig>(config, JsonSerializerOptionsProvider.Default);
        if (apiConfig == null)
        {
            return new SchemaDetectionResult
            {
                Success = false,
                Message = "Invalid API configuration"
            };
        }

        try
        {
            _ssrfGuard.ValidateUrl(apiConfig.BaseUrl!);

            // Create HTTP client
            var httpClient = new HttpClient
            {
                BaseAddress = new Uri(apiConfig.BaseUrl!),
                Timeout = TimeSpan.FromSeconds(apiConfig.TimeoutSeconds)
            };

            // Set default headers
            httpClient.DefaultRequestHeaders.Add("User-Agent", "MultiTenantETL/1.0");
            httpClient.DefaultRequestHeaders.Add("Accept", "*/*");

            // Add authentication
            if (!string.IsNullOrEmpty(apiConfig.AuthType))
            {
                var authType = apiConfig.AuthType.ToLower().Replace(" ", "");
                switch (authType)
                {
                    case "bearer":
                        string? token = null;

                        // Check if dynamic token generation is enabled
                        if (apiConfig.UseDynamicToken)
                        {
                            var tokenResult = await GenerateDynamicTokenForSchemaAsync(apiConfig);
                            if (!tokenResult.Success)
                            {
                                return new SchemaDetectionResult
                                {
                                    Success = false,
                                    Message = $"Failed to generate token: {tokenResult.Message}"
                                };
                            }
                            token = tokenResult.Token;
                        }
                        else
                        {
                            token = apiConfig.AuthToken;
                        }

                        if (!string.IsNullOrEmpty(token))
                        {
                            httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
                        }
                        break;

                    case "basic":
                        if (!string.IsNullOrEmpty(apiConfig.Username) && !string.IsNullOrEmpty(apiConfig.Password))
                        {
                            var credentials = Convert.ToBase64String(
                                System.Text.Encoding.ASCII.GetBytes($"{apiConfig.Username}:{apiConfig.Password}"));
                            httpClient.DefaultRequestHeaders.Add("Authorization", $"Basic {credentials}");
                        }
                        break;

                    case "apikey":
                        if (!string.IsNullOrEmpty(apiConfig.ApiKeyHeader) && !string.IsNullOrEmpty(apiConfig.ApiKeyValue))
                        {
                            httpClient.DefaultRequestHeaders.Add(apiConfig.ApiKeyHeader, apiConfig.ApiKeyValue);
                        }
                        break;
                }
            }

            // Add custom headers
            if (apiConfig.Headers != null)
            {
                foreach (var header in apiConfig.Headers)
                {
                    httpClient.DefaultRequestHeaders.TryAddWithoutValidation(header.Key, header.Value);
                }
            }

            // Call the API endpoint
            var response = await httpClient.GetAsync(endpointPath);

            if (!response.IsSuccessStatusCode)
            {
                return new SchemaDetectionResult
                {
                    Success = false,
                    Message = $"API returned status code {response.StatusCode}"
                };
            }

            var responseContent = await response.Content.ReadAsStringAsync();
            var responseJson = JsonSerializer.Deserialize<JsonElement>(responseContent);

            // Find the endpoint configuration to get the response data path
            var endpoint = apiConfig.Endpoints?.FirstOrDefault(e => e.Path == endpointPath);
            var dataPath = endpoint?.ResponseDataPath ?? "data";

            // Extract data from response using the path
            var dataElement = ExtractDataFromResponse(responseJson, dataPath);

            // Infer schema from the data
            var fields = InferSchemaFromJson(dataElement);

            if (fields.Count == 0)
            {
                return new SchemaDetectionResult
                {
                    Success = false,
                    Message = "No fields could be detected from the API response"
                };
            }

            var schema = new
            {
                fields = fields,
                version = 1,
                detectedAt = DateTime.UtcNow
            };

            return new SchemaDetectionResult
            {
                Success = true,
                Message = $"Successfully detected {fields.Count} fields from API response",
                Schema = JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(schema))
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "API schema detection failed for endpoint {Endpoint}", endpointPath);
            return new SchemaDetectionResult
            {
                Success = false,
                Message = $"Schema detection failed: {ex.Message}"
            };
        }
    }

    private async Task<(bool Success, string? Token, string Message)> GenerateDynamicTokenForSchemaAsync(ApiConfig apiConfig)
    {
        if (string.IsNullOrEmpty(apiConfig.TokenEndpointUrl))
        {
            return (false, null, "Token endpoint URL is required");
        }

        try
        {
            _ssrfGuard.ValidateUrl(apiConfig.TokenEndpointUrl);

            var tokenClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            tokenClient.DefaultRequestHeaders.Add("User-Agent", "MultiTenantETL/1.0");
            tokenClient.DefaultRequestHeaders.Add("Accept", "*/*");

            var request = new HttpRequestMessage
            {
                Method = apiConfig.TokenEndpointMethod?.ToUpper() == "GET" ? HttpMethod.Get : HttpMethod.Post,
                RequestUri = new Uri(apiConfig.TokenEndpointUrl)
            };

            if (request.Method == HttpMethod.Post && !string.IsNullOrEmpty(apiConfig.TokenEndpointBody))
            {
                request.Content = new StringContent(
                    apiConfig.TokenEndpointBody,
                    System.Text.Encoding.UTF8,
                    "application/json");
            }

            if (apiConfig.TokenEndpointHeaders != null)
            {
                foreach (var header in apiConfig.TokenEndpointHeaders)
                {
                    if (header.Key.Equals("Content-Type", StringComparison.OrdinalIgnoreCase))
                        continue;
                    request.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }
            }

            var response = await tokenClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                return (false, null, $"Token endpoint returned {response.StatusCode}");
            }

            var responseContent = await response.Content.ReadAsStringAsync();
            var tokenJson = JsonSerializer.Deserialize<JsonElement>(responseContent);
            var tokenPath = apiConfig.TokenResponsePath ?? "access_token";
            var token = ExtractTokenFromResponse(tokenJson, tokenPath);

            if (string.IsNullOrEmpty(token))
            {
                return (false, null, $"Could not extract token from response using path '{tokenPath}'");
            }

            return (true, token, "Token generated successfully");
        }
        catch (Exception ex)
        {
            return (false, null, $"Token generation failed: {ex.Message}");
        }
    }

    private static string? ExtractTokenFromResponse(JsonElement json, string path)
    {
        try
        {
            if (!path.Contains('.'))
            {
                if (json.TryGetProperty(path, out var value))
                {
                    return value.GetString();
                }
                return null;
            }

            var parts = path.Split('.');
            var current = json;

            foreach (var part in parts)
            {
                if (current.TryGetProperty(part, out var next))
                {
                    current = next;
                }
                else
                {
                    return null;
                }
            }

            return current.GetString();
        }
        catch
        {
            // Token extraction failed, likely invalid path or JSON structure
            return null;
        }
    }

    private static JsonElement ExtractDataFromResponse(JsonElement json, string path)
    {
        try
        {
            if (string.IsNullOrEmpty(path) || path == ".")
            {
                return json;
            }

            if (!path.Contains('.'))
            {
                if (json.TryGetProperty(path, out var value))
                {
                    return value;
                }
                return json;
            }

            var parts = path.Split('.');
            var current = json;

            foreach (var part in parts)
            {
                if (current.TryGetProperty(part, out var next))
                {
                    current = next;
                }
                else
                {
                    return json;
                }
            }

            return current;
        }
        catch
        {
            // Path navigation failed, return original JSON
            return json;
        }
    }

    private static List<SchemaField> InferSchemaFromJson(JsonElement data)
    {
        var fields = new List<SchemaField>();

        // If it's an array, use the first element
        if (data.ValueKind == JsonValueKind.Array && data.GetArrayLength() > 0)
        {
            data = data[0];
        }

        // If it's an object, extract properties
        if (data.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in data.EnumerateObject())
            {
                var field = new SchemaField
                {
                    Name = property.Name,
                    DataType = InferDataType(property.Value),
                    IsNullable = property.Value.ValueKind == JsonValueKind.Null,
                    IsPrimaryKey = property.Name.Equals("id", StringComparison.OrdinalIgnoreCase) ||
                                   property.Name.EndsWith("Id", StringComparison.OrdinalIgnoreCase)
                };

                fields.Add(field);
            }
        }

        return fields;
    }

    private static string InferDataType(JsonElement value)
    {
        return value.ValueKind switch
        {
            JsonValueKind.String => "varchar",
            JsonValueKind.Number => value.TryGetInt32(out _) ? "int" : "decimal",
            JsonValueKind.True or JsonValueKind.False => "boolean",
            JsonValueKind.Array => "json",
            JsonValueKind.Object => "json",
            JsonValueKind.Null => "varchar",
            _ => "varchar"
        };
    }

    /// <summary>
    /// Normalizes database-specific data types to standardized types defined in MetadataConstants.
    /// This ensures consistent type representation across different database providers.
    /// </summary>
    /// <param name="dbType">The database-specific type name</param>
    /// <returns>The normalized type name from MetadataConstants.DataTypes</returns>
    private static string NormalizeDataType(string? dbType)
    {
        if (string.IsNullOrWhiteSpace(dbType))
            return "varchar";

        var normalizedInput = dbType.Trim().ToLowerInvariant();

        // Database-specific type mappings to MetadataConstants standard types
        return normalizedInput switch
        {
            // PostgreSQL types → Standard types
            "integer" or "int4" => "int",
            "int2" => "smallint",
            "int8" => "bigint",
            "smallserial" => "smallint",
            "serial" => "int",
            "bigserial" => "bigint",
            "double precision" => "decimal",
            "real" => "float",
            "character varying" => "varchar",
            "character" => "char",
            "timestamp without time zone" or "timestamp with time zone" => "timestamp",
            "time without time zone" or "time with time zone" => "time",
            "bytea" => "varbinary",

            // MySQL types → Standard types
            "tinyint unsigned" => "smallint",
            "smallint unsigned" => "int",
            "mediumint" or "mediumint unsigned" => "int",
            "int unsigned" => "bigint",
            "bigint unsigned" => "bigint",
            "longtext" or "mediumtext" => "text",
            "tinytext" => "varchar",
            "longblob" or "mediumblob" or "tinyblob" or "blob" => "varbinary",

            // Oracle types → Standard types
            "number" or "numeric" or "bignumeric" => "decimal",
            "varchar2" => "varchar",
            "nvarchar2" => "nvarchar",
            "clob" or "long" => "text",
            "nclob" => "ntext",
            "raw" or "long raw" => "varbinary",

            // SQL Server types (most are already standard, but handle some variations)
            "nvarchar(max)" => "ntext",
            "varchar(max)" => "text",

            // MongoDB common types → Standard types
            "string" => "varchar",  // MongoDB
            "bytes" or "binary" => "varbinary",  // MongoDB
            "int32" => "int",  // MongoDB
            "int64" => "bigint",  // MongoDB
            "float" or "float64" or "double" => "decimal",  // MongoDB
            "bool" => "boolean",
            "record" or "struct" or "document" or "array" => "json",  // MongoDB
            "objectid" => "varchar",  // MongoDB

            // If the type is already in MetadataConstants.DataTypes, return as-is
            _ when MetadataConstants.DataTypes.Types.Any(t => t.Value.Equals(normalizedInput, StringComparison.OrdinalIgnoreCase))
                => MetadataConstants.DataTypes.Types.First(t => t.Value.Equals(normalizedInput, StringComparison.OrdinalIgnoreCase)).Value,

            // Unknown types default to varchar
            _ => "varchar"
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

    private static string BuildOracleConnectionString(DatabaseConfig config)
    {
        if (!string.IsNullOrEmpty(config.ConnectionString))
        {
            return config.ConnectionString;
        }

        var port = config.Port > 0 ? config.Port : 1521;
        var dataSource = $"(DESCRIPTION=(ADDRESS=(PROTOCOL=TCP)(HOST={config.Host})(PORT={port}))(CONNECT_DATA=(SERVICE_NAME={config.Database})))";

        var builder = new OracleConnectionStringBuilder
        {
            DataSource = dataSource,
            UserID = config.Username!,
            Password = config.Password!
        };

        return builder.ConnectionString;
    }

    private async Task<List<SchemaField>> DetectMongoDbSchemaAsync(DatabaseConfig config, string tableName)
    {
        var client = new MongoDB.Driver.MongoClient(config.ConnectionString);
        var database = client.GetDatabase(config.Database);
        var collection = database.GetCollection<MongoDB.Bson.BsonDocument>(tableName);

        // Fetch a few documents to sample types
        var sampleDocs = await collection.Find(MongoDB.Driver.FilterDefinition<MongoDB.Bson.BsonDocument>.Empty)
            .Limit(10)
            .ToListAsync();

        if (sampleDocs.Count == 0) return new List<SchemaField>();

        var fieldMap = new Dictionary<string, SchemaField>();
        foreach (var doc in sampleDocs)
        {
            foreach (var element in doc.Elements)
            {
                if (fieldMap.ContainsKey(element.Name)) continue;

                fieldMap[element.Name] = new SchemaField
                {
                    Name = element.Name,
                    DataType = NormalizeDataType(element.Value.BsonType.ToString()),
                    IsNullable = true,
                    IsPrimaryKey = element.Name == "_id"
                };
            }
        }

        return fieldMap.Values.ToList();
    }

    private async Task<List<SchemaField>> DetectCosmosDbSchemaAsync(DatabaseConfig config, string tableName)
    {
        var endpoint = config.CosmosEndpoint ?? config.Host;
        var key = config.CosmosKey ?? config.Password;

        using var client = new Microsoft.Azure.Cosmos.CosmosClient(endpoint, key);
        var container = client.GetContainer(config.Database, tableName);

        // Fetch a few documents to sample types
        var query = new Microsoft.Azure.Cosmos.QueryDefinition("SELECT TOP 10 * FROM c");
        using var iterator = container.GetItemQueryIterator<System.Text.Json.Nodes.JsonObject>(query);

        if (!iterator.HasMoreResults) return new List<SchemaField>();

        var response = await iterator.ReadNextAsync();
        if (response.Count == 0) return new List<SchemaField>();

        var fieldMap = new Dictionary<string, SchemaField>();
        foreach (var doc in response)
        {
            foreach (var property in doc)
            {
                if (fieldMap.ContainsKey(property.Key)) continue;

                fieldMap[property.Key] = new SchemaField
                {
                    Name = property.Key,
                    DataType = InferDataTypeFromValue(property.Value),
                    IsNullable = true,
                    IsPrimaryKey = property.Key == "id"
                };
            }
        }

        return fieldMap.Values.ToList();
    }

    private string InferDataTypeFromValue(System.Text.Json.Nodes.JsonNode? node)
    {
        if (node == null) return "string";

        var value = node.AsValue();
        if (value.TryGetValue<int>(out _)) return "int";
        if (value.TryGetValue<long>(out _)) return "bigint";
        if (value.TryGetValue<double>(out _)) return "decimal";
        if (value.TryGetValue<bool>(out _)) return "boolean";
        if (value.TryGetValue<DateTime>(out _)) return "datetime";

        return "string";
    }
}
