using Microsoft.Azure.Cosmos;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
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
using System.Text.RegularExpressions;

namespace MultiTenantETL.Infrastructure.Services.ConnectionTesting.Database;

public class DatabaseConnectionTester : IDatabaseConnectionTester
{
    private readonly ISsrfGuard _ssrfGuard;
    private readonly ILogger<DatabaseConnectionTester> _logger;

    public DatabaseConnectionTester(ILogger<DatabaseConnectionTester> logger, ISsrfGuard ssrfGuard)
    {
        _logger = logger;
        _ssrfGuard = ssrfGuard;
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
                 provider != ConnectorProviders.MongoDb)
        {
            // Standard SQL database providers require host, database, username, password
            // CosmosDB and MongoDB have their own validation
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
            // Validate the target host/endpoint against SSRF-protected ranges
            // before attempting any connection.
            ValidateDatabaseConfig(provider, dbConfig);

            return provider switch
            {
                ConnectorProviders.SqlServer => await TestSqlServerConnectionAsync(dbConfig),
                ConnectorProviders.PostgreSQL => await TestPostgreSqlConnectionAsync(dbConfig),
                ConnectorProviders.MySQL => await TestMySqlConnectionAsync(dbConfig),
                ConnectorProviders.Oracle => await TestOracleConnectionAsync(dbConfig),
                ConnectorProviders.MongoDb => await TestMongoDbConnectionAsync(dbConfig),
                ConnectorProviders.CosmosDb => await TestCosmosDbConnectionAsync(dbConfig),
                _ => new ConnectionTestResult
                {
                    Success = false,
                    Message = $"Database provider {provider} is not supported"
                }
            };
        }
        catch (SsrfBlockedException ex)
        {
            _logger.LogWarning("SSRF validation blocked database connection test for provider {Provider}: {Message}", provider, ex.Message);
            return new ConnectionTestResult
            {
                Success = false,
                Message = ex.Message
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

    private void ValidateDatabaseConfig(string provider, DatabaseConfig config)
    {
        string? host = null;

        switch (provider)
        {
            case ConnectorProviders.CosmosDb:
                var endpoint = config.CosmosEndpoint ?? config.Host;
                if (!string.IsNullOrWhiteSpace(endpoint))
                {
                    _ssrfGuard.ValidateUrl(endpoint);
                }
                return;

            case ConnectorProviders.MongoDb:
                if (!string.IsNullOrWhiteSpace(config.ConnectionString))
                {
                    var (isSrv, mongoHosts) = ExtractMongoHosts(config.ConnectionString);
                    foreach (var mongoHost in mongoHosts)
                    {
                        // mongodb+srv hostnames have no A/AAAA record (SRV-only),
                        // so they cannot be resolved to IPs here; apply name-based
                        // checks only and document the residual SRV-target gap.
                        if (isSrv)
                        {
                            _ssrfGuard.ValidateHostName(mongoHost);
                        }
                        else
                        {
                            _ssrfGuard.ValidateHost(mongoHost);
                        }
                    }
                }
                break;

            case ConnectorProviders.SqlServer:
            case ConnectorProviders.PostgreSQL:
            case ConnectorProviders.MySQL:
            case ConnectorProviders.Oracle:
                host = !string.IsNullOrWhiteSpace(config.ConnectionString)
                    ? ExtractHostFromConnectionString(provider, config.ConnectionString)
                    : config.Host;
                break;
        }

        if (!string.IsNullOrWhiteSpace(host))
        {
            _ssrfGuard.ValidateHost(host);
        }
    }

    private static (bool IsSrv, string[] Hosts) ExtractMongoHosts(string connectionString)
    {
        var hosts = new List<string>();

        var isSrv = connectionString.StartsWith("mongodb+srv://", StringComparison.OrdinalIgnoreCase);
        if (!isSrv && !connectionString.StartsWith("mongodb://", StringComparison.OrdinalIgnoreCase))
        {
            return (false, hosts.ToArray());
        }

        var schemeLength = isSrv ? "mongodb+srv://".Length : "mongodb://".Length;

        // Everything up to the first '/', '?' or '#' is the host section.
        var remainder = connectionString.Substring(schemeLength);
        var end = remainder.IndexOfAny(new[] { '/', '?', '#' });
        if (end >= 0)
        {
            remainder = remainder.Substring(0, end);
        }

        // Strip any userinfo ("user:password@").
        var at = remainder.LastIndexOf('@');
        if (at >= 0)
        {
            remainder = remainder.Substring(at + 1);
        }

        if (string.IsNullOrWhiteSpace(remainder))
        {
            return (isSrv, hosts.ToArray());
        }

        if (isSrv)
        {
            // SRV hostnames have no port or seed list.
            hosts.Add(remainder.Trim('[', ']'));
            return (true, hosts.ToArray());
        }

        // mongodb:// supports a comma-separated seed list; uri.Host would only
        // expose the first seed, so validate every host the driver may connect to.
        foreach (var hostPort in remainder.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            var hp = hostPort.Trim();
            string hostOnly;

            if (hp.StartsWith('['))
            {
                var close = hp.IndexOf(']');
                hostOnly = close > 0 ? hp.Substring(1, close - 1) : hp.Trim('[', ']');
            }
            else
            {
                var colon = hp.IndexOf(':');
                hostOnly = colon > 0 ? hp.Substring(0, colon) : hp;
            }

            if (!string.IsNullOrWhiteSpace(hostOnly))
            {
                hosts.Add(hostOnly);
            }
        }

        return (false, hosts.ToArray());
    }

    private static string? ExtractHostFromConnectionString(string provider, string connectionString)
    {
        try
        {
            switch (provider)
            {
                case ConnectorProviders.SqlServer:
                    var sqlBuilder = new SqlConnectionStringBuilder(connectionString);
                    return sqlBuilder.DataSource?.Split(',')[0].Trim();
                case ConnectorProviders.PostgreSQL:
                    var npgBuilder = new NpgsqlConnectionStringBuilder(connectionString);
                    return npgBuilder.Host;
                case ConnectorProviders.MySQL:
                    var mySqlBuilder = new MySqlConnectionStringBuilder(connectionString);
                    return mySqlBuilder.Server;
                case ConnectorProviders.Oracle:
                    var oracleBuilder = new OracleConnectionStringBuilder(connectionString);
                    return ExtractHostFromOracleDataSource(oracleBuilder.DataSource);
            }
        }
        catch
        {
            // Fall through to the generic regex extraction.
        }

        var match = Regex.Match(connectionString,
            @"(?:Data\s*Source|Server|Host|Address)\s*=\s*([^;,\s]+)",
            RegexOptions.IgnoreCase);

        return match.Success ? match.Groups[1].Value.Trim() : null;
    }

    private static string? ExtractHostFromOracleDataSource(string? dataSource)
    {
        if (string.IsNullOrWhiteSpace(dataSource))
        {
            return null;
        }

        var match = Regex.Match(dataSource, @"HOST\s*=\s*([^\)\s]+)", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value.Trim() : dataSource.Trim();
    }
}
