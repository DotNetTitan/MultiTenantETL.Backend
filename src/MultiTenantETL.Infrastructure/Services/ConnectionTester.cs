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

public class ConnectionTester : IConnectionTester
{
    private readonly ILogger<ConnectionTester> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public ConnectionTester(ILogger<ConnectionTester> logger, IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<ConnectionTestResult> TestConnectionAsync(string type, string provider, JsonElement config)
    {
        try
        {
            return type switch
            {
                ConnectorTypes.Database => await TestDatabaseConnectionAsync(provider, config),
                ConnectorTypes.File => TestFileConnectionAsync(provider, config),
                ConnectorTypes.Api => await TestApiConnectionAsync(provider, config),
                _ => new ConnectionTestResult
                {
                    Success = false,
                    Message = $"Unsupported connector type: {type}"
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Connection test failed for type {Type}, provider {Provider}", type, provider);
            return new ConnectionTestResult
            {
                Success = false,
                Message = $"Connection test failed: {ex.Message}"
            };
        }
    }

    private async Task<ConnectionTestResult> TestDatabaseConnectionAsync(string provider, JsonElement config)
    {
        var dbConfig = JsonSerializer.Deserialize<DatabaseConfig>(config, JsonOptions);
        if (dbConfig == null)
        {
            return new ConnectionTestResult
            {
                Success = false,
                Message = "Invalid database configuration"
            };
        }

        // Validate required fields
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
        else
        {
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

    private async Task<ConnectionTestResult> TestSqlServerConnectionAsync(DatabaseConfig config)
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

    private async Task<ConnectionTestResult> TestPostgreSqlConnectionAsync(DatabaseConfig config)
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

    private async Task<ConnectionTestResult> TestMySqlConnectionAsync(DatabaseConfig config)
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

    private ConnectionTestResult TestFileConnectionAsync(string provider, JsonElement config)
    {
        var fileConfig = JsonSerializer.Deserialize<FileConfig>(config, JsonOptions);
        if (fileConfig == null)
        {
            return new ConnectionTestResult
            {
                Success = false,
                Message = "Invalid file configuration"
            };
        }

        // Validate required fields
        if (string.IsNullOrEmpty(fileConfig.Path))
        {
            return new ConnectionTestResult
            {
                Success = false,
                Message = "File path is required"
            };
        }

        try
        {
            // For local files, check if path exists
            if (provider == "Local")
            {
                if (!File.Exists(fileConfig.Path) && !Directory.Exists(fileConfig.Path))
                {
                    return new ConnectionTestResult
                    {
                        Success = false,
                        Message = $"File or directory not found: {fileConfig.Path}"
                    };
                }

                var isDirectory = Directory.Exists(fileConfig.Path);
                var details = new Dictionary<string, object>
                {
                    ["Path"] = fileConfig.Path,
                    ["Type"] = isDirectory ? "Directory" : "File",
                    ["Exists"] = true
                };

                if (!isDirectory)
                {
                    var fileInfo = new FileInfo(fileConfig.Path);
                    details["Size"] = fileInfo.Length;
                    details["LastModified"] = fileInfo.LastWriteTimeUtc;
                }

                return new ConnectionTestResult
                {
                    Success = true,
                    Message = $"Successfully validated {provider} file path",
                    Details = details
                };
            }
            else
            {
                // For remote providers (FTP, S3, Azure), just validate configuration
                return new ConnectionTestResult
                {
                    Success = true,
                    Message = $"Configuration validated for {provider} provider. Full connection test will be performed during pipeline execution.",
                    Details = new Dictionary<string, object>
                    {
                        ["Provider"] = provider,
                        ["Path"] = fileConfig.Path,
                        ["Format"] = fileConfig.Format ?? "Unknown"
                    }
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "File connection test failed for provider {Provider}", provider);
            return new ConnectionTestResult
            {
                Success = false,
                Message = $"File validation failed: {ex.Message}"
            };
        }
    }

    private async Task<ConnectionTestResult> TestApiConnectionAsync(string provider, JsonElement config)
    {
        var apiConfig = JsonSerializer.Deserialize<ApiConfig>(config, JsonOptions);
        if (apiConfig == null)
        {
            return new ConnectionTestResult
            {
                Success = false,
                Message = "Invalid API configuration"
            };
        }

        // Validate required fields
        if (string.IsNullOrEmpty(apiConfig.BaseUrl))
        {
            return new ConnectionTestResult
            {
                Success = false,
                Message = "Base URL is required"
            };
        }

        try
        {
            var httpClient = _httpClientFactory.CreateClient();
            httpClient.Timeout = TimeSpan.FromSeconds(apiConfig.TimeoutSeconds);
            httpClient.BaseAddress = new Uri(apiConfig.BaseUrl);

            // Add authentication
            if (!string.IsNullOrEmpty(apiConfig.AuthType))
            {
                var authType = apiConfig.AuthType.ToLower().Replace(" ", "");
                switch (authType)
                {
                    case "bearer":
                        if (!string.IsNullOrEmpty(apiConfig.AuthToken))
                        {
                            httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiConfig.AuthToken}");
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
                    httpClient.DefaultRequestHeaders.Add(header.Key, header.Value);
                }
            }

            // Test connection using first GET endpoint if available, otherwise test root
            string testPath = "/";
            if (apiConfig.Endpoints != null && apiConfig.Endpoints.Count > 0)
            {
                var getEndpoint = apiConfig.Endpoints.FirstOrDefault(e => e.Method.Equals("GET", StringComparison.OrdinalIgnoreCase));
                if (getEndpoint != null)
                {
                    testPath = getEndpoint.Path;
                }
            }
            
            var response = await httpClient.GetAsync(testPath);
            
            var details = new Dictionary<string, object>
            {
                ["StatusCode"] = (int)response.StatusCode,
                ["IsSuccessStatusCode"] = response.IsSuccessStatusCode,
                ["BaseUrl"] = apiConfig.BaseUrl,
                ["TestPath"] = testPath
            };

            return new ConnectionTestResult
            {
                Success = response.IsSuccessStatusCode,
                Message = response.IsSuccessStatusCode 
                    ? $"Successfully connected to API at {testPath}" 
                    : $"API returned status code {response.StatusCode} for {testPath}",
                Details = details
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "API connection test failed");
            return new ConnectionTestResult
            {
                Success = false,
                Message = $"API connection failed: {ex.Message}"
            };
        }
    }

    // Connection string builders
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
}
