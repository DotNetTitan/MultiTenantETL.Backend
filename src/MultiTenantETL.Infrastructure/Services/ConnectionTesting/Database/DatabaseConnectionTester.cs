using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using MultiTenantETL.Application.Connectors;
using MultiTenantETL.Application.Connectors.Models;
using MultiTenantETL.Domain.Constants;
using MultiTenantETL.Infrastructure.Configuration;
using Npgsql;
using MySqlConnector;
using Oracle.ManagedDataAccess.Client;
using Snowflake.Data.Client;
using MongoDB.Driver;
using MongoDB.Bson;
using Microsoft.Azure.Cosmos;

namespace MultiTenantETL.Infrastructure.Services.ConnectionTesting.Database;

public class DatabaseConnectionTester : IDatabaseConnectionTester
{
    private readonly ILogger<DatabaseConnectionTester> _logger;

    public DatabaseConnectionTester(ILogger<DatabaseConnectionTester> logger)
    {
        _logger = logger;
    }

    public async Task<ConnectionTestResult> TestConnectionAsync(string provider, JsonElement config)
    {
        var dbConfig = JsonSerializer.Deserialize<DatabaseConfig>(config, JsonSerializerOptionsProvider.Default);
        if (dbConfig == null)
        {
            return new ConnectionTestResult
            {
                Success = false,
                Message = "Invalid database configuration"
            };
        }

        // Validate required fields (skip validation for providers with custom validation)
        if (dbConfig.UseCustomConnectionString)
        {
            if (string.IsNullOrEmpty(dbConfig.ConnectionString))
            {
                return new ConnectionTestResult
                {
                    Success = false,
                    Message = "Connection string is required when using custom connection string"
                };
            }
        }
        else if (provider != ConnectorProviders.CosmosDb && 
                 provider != ConnectorProviders.MongoDb && 
                 provider != ConnectorProviders.BigQuery &&
                 provider != ConnectorProviders.Snowflake)
        {
            // Standard SQL database providers require host, database, username, password
            // CosmosDB, MongoDB, BigQuery, and Snowflake have their own validation
            if (string.IsNullOrEmpty(dbConfig.Host) || string.IsNullOrEmpty(dbConfig.Database) ||
                string.IsNullOrEmpty(dbConfig.Username) || string.IsNullOrEmpty(dbConfig.Password))
            {
                return new ConnectionTestResult
                {
                    Success = false,
                    Message = "Host, database, username, and password are required"
                };
            }
        }

        try
        {
            return provider switch
            {
                ConnectorProviders.SqlServer => await TestSqlServerConnectionAsync(dbConfig),
                ConnectorProviders.PostgreSQL => await TestPostgreSqlConnectionAsync(dbConfig),
                ConnectorProviders.MySQL => await TestMySqlConnectionAsync(dbConfig),
                ConnectorProviders.Oracle => await TestOracleConnectionAsync(dbConfig),
                ConnectorProviders.Snowflake => await TestSnowflakeConnectionAsync(dbConfig),
                ConnectorProviders.BigQuery => await TestBigQueryConnectionAsync(dbConfig),
                ConnectorProviders.Redshift => await TestRedshiftConnectionAsync(dbConfig),
                ConnectorProviders.MongoDb => await TestMongoDbConnectionAsync(dbConfig),
                ConnectorProviders.CosmosDb => await TestCosmosDbConnectionAsync(dbConfig),
                _ => new ConnectionTestResult
                {
                    Success = false,
                    Message = $"Database provider {provider} is not supported"
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database connection test failed for provider {Provider}", provider);
            return new ConnectionTestResult
            {
                Success = false,
                Message = $"Database connection failed: {ex.Message}"
            };
        }
    }

    private static async Task<ConnectionTestResult> TestSqlServerConnectionAsync(DatabaseConfig config)
    {
        using var connection = new SqlConnection(BuildSqlServerConnectionString(config));
        await connection.OpenAsync();
        
        var details = new Dictionary<string, object>
        {
            ["ServerVersion"] = connection.ServerVersion,
            ["Database"] = connection.Database,
            ["State"] = connection.State.ToString()
        };

        return new ConnectionTestResult
        {
            Success = true,
            Message = "Successfully connected to SQL Server database",
            Details = details
        };
    }

    private static async Task<ConnectionTestResult> TestPostgreSqlConnectionAsync(DatabaseConfig config)
    {
        using var connection = new NpgsqlConnection(BuildPostgreSqlConnectionString(config));
        await connection.OpenAsync();
        
        var details = new Dictionary<string, object>
        {
            ["ServerVersion"] = connection.ServerVersion,
            ["Database"] = connection.Database,
            ["State"] = connection.State.ToString()
        };

        return new ConnectionTestResult
        {
            Success = true,
            Message = "Successfully connected to PostgreSQL database",
            Details = details
        };
    }

    private static async Task<ConnectionTestResult> TestMySqlConnectionAsync(DatabaseConfig config)
    {
        using var connection = new MySqlConnection(BuildMySqlConnectionString(config));
        await connection.OpenAsync();
        
        var details = new Dictionary<string, object>
        {
            ["ServerVersion"] = connection.ServerVersion,
            ["Database"] = connection.Database,
            ["State"] = connection.State.ToString()
        };

        return new ConnectionTestResult
        {
            Success = true,
            Message = "Successfully connected to MySQL database",
            Details = details
        };
    }

    private static async Task<ConnectionTestResult> TestOracleConnectionAsync(DatabaseConfig config)
    {
        using var connection = new OracleConnection(BuildOracleConnectionString(config));
        await connection.OpenAsync();

        var details = new Dictionary<string, object>
        {
            ["ServerVersion"] = connection.ServerVersion,
            ["Database"] = connection.DatabaseName ?? "Unknown",
            ["State"] = connection.State.ToString()
        };

        return new ConnectionTestResult
        {
            Success = true,
            Message = "Successfully connected to Oracle database",
            Details = details
        };
    }

    private static async Task<ConnectionTestResult> TestSnowflakeConnectionAsync(DatabaseConfig config)
    {
        // Validate required fields for Snowflake
        if (string.IsNullOrEmpty(config.Account) && string.IsNullOrEmpty(config.Host))
        {
            return new ConnectionTestResult 
            { 
                Success = false, 
                Message = "Snowflake account or host is required" 
            };
        }

        if (string.IsNullOrEmpty(config.Username))
        {
            return new ConnectionTestResult 
            { 
                Success = false, 
                Message = "Username is required for Snowflake" 
            };
        }

        if (string.IsNullOrEmpty(config.Password))
        {
            return new ConnectionTestResult 
            { 
                Success = false, 
                Message = "Password is required for Snowflake" 
            };
        }

        using var connection = new SnowflakeDbConnection(BuildSnowflakeConnectionString(config));
        await connection.OpenAsync();

        var details = new Dictionary<string, object>
        {
            ["ServerVersion"] = connection.ServerVersion,
            ["Database"] = connection.Database ?? "Unknown",
            ["State"] = connection.State.ToString()
        };

        if (!string.IsNullOrEmpty(config.Account))
        {
            details["Account"] = config.Account;
        }
        if (!string.IsNullOrEmpty(config.Warehouse))
        {
            details["Warehouse"] = config.Warehouse;
        }

        return new ConnectionTestResult
        {
            Success = true,
            Message = "Successfully connected to Snowflake database",
            Details = details
        };
    }

    private static async Task<ConnectionTestResult> TestBigQueryConnectionAsync(DatabaseConfig config)
    {
        if (string.IsNullOrEmpty(config.ProjectId))
        {
            return new ConnectionTestResult { Success = false, Message = "Project ID is required" };
        }

        if (string.IsNullOrEmpty(config.DatasetId))
        {
            return new ConnectionTestResult { Success = false, Message = "Dataset ID is required" };
        }

        Google.Cloud.BigQuery.V2.BigQueryClient client;
        if (!string.IsNullOrEmpty(config.JsonCredentials))
        {
            var credential = Google.Apis.Auth.OAuth2.GoogleCredential.FromJson(config.JsonCredentials);
            client = Google.Cloud.BigQuery.V2.BigQueryClient.Create(config.ProjectId, credential);
        }
        else
        {
            client = Google.Cloud.BigQuery.V2.BigQueryClient.Create(config.ProjectId);
        }

        await client.GetDatasetAsync(config.DatasetId);

        return new ConnectionTestResult
        {
            Success = true,
            Message = "Successfully connected to Google BigQuery",
            Details = new Dictionary<string, object>
            {
                ["ProjectId"] = config.ProjectId,
                ["DatasetId"] = config.DatasetId
            }
        };
    }

    private static async Task<ConnectionTestResult> TestRedshiftConnectionAsync(DatabaseConfig config)
    {
        using var connection = new NpgsqlConnection(BuildRedshiftConnectionString(config));
        await connection.OpenAsync();
        
        var details = new Dictionary<string, object>
        {
            ["ServerVersion"] = connection.ServerVersion,
            ["Database"] = connection.Database,
            ["State"] = connection.State.ToString()
        };

        return new ConnectionTestResult
        {
            Success = true,
            Message = "Successfully connected to AWS Redshift",
            Details = details
        };
    }

    private static async Task<ConnectionTestResult> TestMongoDbConnectionAsync(DatabaseConfig config)
    {
        if (string.IsNullOrEmpty(config.ConnectionString))
        {
            return new ConnectionTestResult { Success = false, Message = "Connection string is required for MongoDB" };
        }

        var client = new MongoClient(config.ConnectionString);
        var database = client.GetDatabase(config.Database ?? "admin");
        await database.RunCommandAsync((Command<BsonDocument>)"{ping:1}");

        return new ConnectionTestResult
        {
            Success = true,
            Message = "Successfully connected to MongoDB",
            Details = new Dictionary<string, object>
            {
                ["Database"] = database.DatabaseNamespace.DatabaseName
            }
        };
    }

    private static async Task<ConnectionTestResult> TestCosmosDbConnectionAsync(DatabaseConfig config)
    {
        var endpoint = config.CosmosEndpoint ?? config.Host;
        var key = config.CosmosKey ?? config.Password;
        var database = config.Database;
        var container = config.Container;

        // Validate required fields for CosmosDB
        if (string.IsNullOrEmpty(endpoint))
        {
            return new ConnectionTestResult { Success = false, Message = "Cosmos DB endpoint is required" };
        }

        if (string.IsNullOrEmpty(key))
        {
            return new ConnectionTestResult { Success = false, Message = "Cosmos DB key is required" };
        }

        if (string.IsNullOrEmpty(database))
        {
            return new ConnectionTestResult { Success = false, Message = "Cosmos DB database name is required" };
        }

        if (string.IsNullOrEmpty(container))
        {
            return new ConnectionTestResult { Success = false, Message = "Cosmos DB container name is required" };
        }

        using var client = new CosmosClient(endpoint, key);
        
        // Test connection by reading account info
        await client.ReadAccountAsync();

        // Verify database and container exist
        var db = client.GetDatabase(database);
        var containerResponse = await db.GetContainer(container).ReadContainerAsync();

        return new ConnectionTestResult
        {
            Success = true,
            Message = "Successfully connected to Azure Cosmos DB",
            Details = new Dictionary<string, object>
            {
                ["Endpoint"] = endpoint,
                ["Database"] = database,
                ["Container"] = container,
                ["Throughput"] = containerResponse.Resource.Id ?? "N/A"
            }
        };
    }

    private static string BuildSqlServerConnectionString(DatabaseConfig config)
    {
        if (!string.IsNullOrEmpty(config.ConnectionString))
        {
            return config.ConnectionString;
        }

        var port = config.Port > 0 ? config.Port : 1433;
        var builder = new SqlConnectionStringBuilder
        {
            DataSource = $"{config.Host},{port}",
            InitialCatalog = config.Database!,
            UserID = config.Username!,
            Password = config.Password!,
            TrustServerCertificate = !config.UseSsl,
            Encrypt = config.UseSsl
        };

        return builder.ConnectionString;
    }

    private static string BuildPostgreSqlConnectionString(DatabaseConfig config)
    {
        if (!string.IsNullOrEmpty(config.ConnectionString))
        {
            return config.ConnectionString;
        }

        var port = config.Port > 0 ? config.Port : 5432;
        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = config.Host!,
            Port = port,
            Database = config.Database!,
            Username = config.Username!,
            Password = config.Password!,
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

        var port = config.Port > 0 ? (uint)config.Port : 3306;
        var builder = new MySqlConnectionStringBuilder
        {
            Server = config.Host!,
            Port = port,
            Database = config.Database!,
            UserID = config.Username!,
            Password = config.Password!,
            SslMode = config.UseSsl ? MySqlSslMode.Required : MySqlSslMode.Preferred
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

    private static string BuildSnowflakeConnectionString(DatabaseConfig config)
    {
        if (!string.IsNullOrEmpty(config.ConnectionString))
        {
            return config.ConnectionString;
        }

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

    private static string BuildRedshiftConnectionString(DatabaseConfig config)
    {
        if (!string.IsNullOrEmpty(config.ConnectionString))
        {
            return config.ConnectionString;
        }

        var port = config.Port > 0 ? config.Port : 5439;
        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = config.Host!,
            Port = port,
            Database = config.Database!,
            Username = config.Username!,
            Password = config.Password!,
            SslMode = SslMode.Require
        };

        return builder.ConnectionString;
    }
}
