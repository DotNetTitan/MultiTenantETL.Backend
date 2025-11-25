using System.Data;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using MultiTenantETL.Application.Connectors;
using MultiTenantETL.Application.Connectors.Models;
using MultiTenantETL.Domain.Constants;
using Npgsql;
using MySqlConnector;
using Azure.Storage.Blobs;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.Runtime;
using FluentFTP;
using Renci.SshNet;

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
                ConnectorTypes.File => await TestFileConnectionAsync(provider, config),
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

    private async Task<ConnectionTestResult> TestFileConnectionAsync(string provider, JsonElement config)
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
            return provider switch
            {
                "Local" => TestLocalFileConnection(fileConfig),
                "FTP" => await TestFtpConnectionAsync(fileConfig),
                "SFTP" => await TestSftpConnectionAsync(fileConfig),
                "S3" => await TestS3ConnectionAsync(fileConfig),
                "AzureBlob" => await TestAzureBlobConnectionAsync(fileConfig),
                _ => new ConnectionTestResult
                {
                    Success = false,
                    Message = $"Unsupported file provider: {provider}"
                }
            };
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

    private ConnectionTestResult TestLocalFileConnection(FileConfig config)
    {
        if (!File.Exists(config.Path) && !Directory.Exists(config.Path))
        {
            return new ConnectionTestResult
            {
                Success = false,
                Message = $"File or directory not found: {config.Path}"
            };
        }

        var isDirectory = Directory.Exists(config.Path);
        var details = new Dictionary<string, object>
        {
            ["Path"] = config.Path,
            ["Type"] = isDirectory ? "Directory" : "File",
            ["Exists"] = true
        };

        if (!isDirectory)
        {
            var fileInfo = new FileInfo(config.Path);
            details["Size"] = fileInfo.Length;
            details["LastModified"] = fileInfo.LastWriteTimeUtc;
        }

        return new ConnectionTestResult
        {
            Success = true,
            Message = "Successfully validated local file path",
            Details = details
        };
    }

    private async Task<ConnectionTestResult> TestAzureBlobConnectionAsync(FileConfig config)
    {
        // Validate required Azure fields
        if (string.IsNullOrEmpty(config.AzureAccountName))
        {
            return new ConnectionTestResult
            {
                Success = false,
                Message = "Azure Storage Account Name is required"
            };
        }

        if (string.IsNullOrEmpty(config.AzureAccountKey))
        {
            return new ConnectionTestResult
            {
                Success = false,
                Message = "Azure Storage Account Key is required"
            };
        }

        if (string.IsNullOrEmpty(config.AzureContainer))
        {
            return new ConnectionTestResult
            {
                Success = false,
                Message = "Azure Container Name is required"
            };
        }

        try
        {
            // Build connection string
            var connectionString = $"DefaultEndpointsProtocol=https;AccountName={config.AzureAccountName};AccountKey={config.AzureAccountKey};EndpointSuffix=core.windows.net";
            
            // Configure retry options - reduce from default 6 to 3 attempts
            var blobClientOptions = new Azure.Storage.Blobs.BlobClientOptions
            {
                Retry = {
                    MaxRetries = 3,
                    Delay = TimeSpan.FromSeconds(1),
                    MaxDelay = TimeSpan.FromSeconds(3),
                    Mode = Azure.Core.RetryMode.Fixed
                }
            };
            
            // Create blob service client with custom retry policy
            var blobServiceClient = new BlobServiceClient(connectionString, blobClientOptions);
            
            // Get container client
            var containerClient = blobServiceClient.GetBlobContainerClient(config.AzureContainer);
            
            // Test connection by checking if container exists
            var exists = await containerClient.ExistsAsync();
            
            if (!exists.Value)
            {
                return new ConnectionTestResult
                {
                    Success = false,
                    Message = $"Container '{config.AzureContainer}' does not exist in storage account '{config.AzureAccountName}'"
                };
            }

            // Get container properties to verify access
            var properties = await containerClient.GetPropertiesAsync();
            
            var details = new Dictionary<string, object>
            {
                ["AccountName"] = config.AzureAccountName,
                ["Container"] = config.AzureContainer,
                ["BlobPath"] = config.Path,
                ["LastModified"] = properties.Value.LastModified,
                ["HasImmutabilityPolicy"] = properties.Value.HasImmutabilityPolicy
            };

            return new ConnectionTestResult
            {
                Success = true,
                Message = $"Successfully connected to Azure Blob Storage container '{config.AzureContainer}'",
                Details = details
            };
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 403)
        {
            return new ConnectionTestResult
            {
                Success = false,
                Message = "Access denied. Please verify your Azure Storage Account Key is correct and has proper permissions."
            };
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 404)
        {
            return new ConnectionTestResult
            {
                Success = false,
                Message = $"Storage account '{config.AzureAccountName}' or container '{config.AzureContainer}' not found."
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Azure Blob connection test failed");
            
            // Extract the most relevant error message
            var errorMessage = GetRootErrorMessage(ex);
            
            return new ConnectionTestResult
            {
                Success = false,
                Message = $"Azure Blob connection failed: {errorMessage}"
            };
        }
    }

    private async Task<ConnectionTestResult> TestS3ConnectionAsync(FileConfig config)
    {
        // Validate required S3 fields
        if (string.IsNullOrEmpty(config.S3AccessKey))
        {
            return new ConnectionTestResult
            {
                Success = false,
                Message = "AWS Access Key ID is required"
            };
        }

        if (string.IsNullOrEmpty(config.S3SecretKey))
        {
            return new ConnectionTestResult
            {
                Success = false,
                Message = "AWS Secret Access Key is required"
            };
        }

        if (string.IsNullOrEmpty(config.S3Bucket))
        {
            return new ConnectionTestResult
            {
                Success = false,
                Message = "S3 Bucket Name is required"
            };
        }

        if (string.IsNullOrEmpty(config.S3Region))
        {
            return new ConnectionTestResult
            {
                Success = false,
                Message = "AWS Region is required"
            };
        }

        try
        {
            // Create S3 client with custom retry policy - reduce from default to 3 attempts
            var s3Config = new Amazon.S3.AmazonS3Config
            {
                MaxErrorRetry = 3,
                Timeout = TimeSpan.FromSeconds(10),
                ForcePathStyle = !string.IsNullOrEmpty(config.S3Endpoint) // MinIO requires path-style
            };
            
            // Use custom endpoint if provided (for MinIO, etc.), otherwise use AWS
            if (!string.IsNullOrEmpty(config.S3Endpoint))
            {
                s3Config.ServiceURL = config.S3Endpoint;
            }
            else
            {
                s3Config.RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(config.S3Region);
            }
            
            var s3Client = new AmazonS3Client(config.S3AccessKey, config.S3SecretKey, s3Config);
            
            // Test connection by checking if bucket exists and is accessible
            var bucketRequest = new GetBucketLocationRequest
            {
                BucketName = config.S3Bucket
            };
            
            var bucketResponse = await s3Client.GetBucketLocationAsync(bucketRequest);
            
            var details = new Dictionary<string, object>
            {
                ["Bucket"] = config.S3Bucket,
                ["Region"] = config.S3Region,
                ["BucketLocation"] = bucketResponse.Location.Value,
                ["ObjectKey"] = config.Path
            };

            return new ConnectionTestResult
            {
                Success = true,
                Message = $"Successfully connected to S3 bucket '{config.S3Bucket}' in region '{config.S3Region}'",
                Details = details
            };
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.Forbidden)
        {
            return new ConnectionTestResult
            {
                Success = false,
                Message = "Access denied. Please verify your AWS credentials have proper permissions for this bucket."
            };
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return new ConnectionTestResult
            {
                Success = false,
                Message = $"S3 bucket '{config.S3Bucket}' not found in region '{config.S3Region}'."
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "S3 connection test failed");
            
            // Extract the most relevant error message
            var errorMessage = GetRootErrorMessage(ex);
            
            return new ConnectionTestResult
            {
                Success = false,
                Message = $"S3 connection failed: {errorMessage}"
            };
        }
    }

    private async Task<ConnectionTestResult> TestFtpConnectionAsync(FileConfig config)
    {
        // Validate required FTP fields
        if (string.IsNullOrEmpty(config.FtpHost))
        {
            return new ConnectionTestResult
            {
                Success = false,
                Message = "FTP Host is required"
            };
        }

        if (string.IsNullOrEmpty(config.FtpUsername))
        {
            return new ConnectionTestResult
            {
                Success = false,
                Message = "FTP Username is required"
            };
        }

        if (string.IsNullOrEmpty(config.FtpPassword))
        {
            return new ConnectionTestResult
            {
                Success = false,
                Message = "FTP Password is required"
            };
        }

        try
        {
            var port = config.FtpPort ?? 21;
            
            using var ftpClient = new AsyncFtpClient(config.FtpHost, config.FtpUsername, config.FtpPassword, port);
            
            // Connect to FTP server
            await ftpClient.Connect();
            
            if (!ftpClient.IsConnected)
            {
                return new ConnectionTestResult
                {
                    Success = false,
                    Message = "Failed to connect to FTP server"
                };
            }

            // Test if path exists (if provided)
            bool pathExists = false;
            string pathType = "Unknown";
            
            if (!string.IsNullOrEmpty(config.Path))
            {
                var fileExists = await ftpClient.FileExists(config.Path);
                var dirExists = await ftpClient.DirectoryExists(config.Path);
                pathExists = fileExists || dirExists;
                pathType = fileExists ? "File" : dirExists ? "Directory" : "Not Found";
            }

            var details = new Dictionary<string, object>
            {
                ["Host"] = config.FtpHost ?? "Unknown",
                ["Port"] = port,
                ["IsConnected"] = ftpClient.IsConnected,
                ["ServerType"] = ftpClient.ServerType.ToString(),
                ["Path"] = config.Path ?? "Not specified",
                ["PathExists"] = pathExists,
                ["PathType"] = pathType
            };

            await ftpClient.Disconnect();

            return new ConnectionTestResult
            {
                Success = true,
                Message = $"Successfully connected to FTP server at {config.FtpHost}:{port}",
                Details = details
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "FTP connection test failed");
            
            // Extract the most relevant error message
            var errorMessage = GetRootErrorMessage(ex);
            
            return new ConnectionTestResult
            {
                Success = false,
                Message = $"FTP connection failed: {errorMessage}"
            };
        }
    }

    private async Task<ConnectionTestResult> TestSftpConnectionAsync(FileConfig config)
    {
        // Validate required SFTP fields
        if (string.IsNullOrEmpty(config.SftpHost))
        {
            return new ConnectionTestResult
            {
                Success = false,
                Message = "SFTP Host is required"
            };
        }

        if (string.IsNullOrEmpty(config.SftpUsername))
        {
            return new ConnectionTestResult
            {
                Success = false,
                Message = "SFTP Username is required"
            };
        }

        if (string.IsNullOrEmpty(config.SftpPassword))
        {
            return new ConnectionTestResult
            {
                Success = false,
                Message = "SFTP Password is required"
            };
        }

        try
        {
            var port = config.SftpPort ?? 22;
            
            using var sftpClient = new SftpClient(config.SftpHost, port, config.SftpUsername, config.SftpPassword);
            
            // Connect to SFTP server
            await Task.Run(() => sftpClient.Connect());
            
            if (!sftpClient.IsConnected)
            {
                return new ConnectionTestResult
                {
                    Success = false,
                    Message = "Failed to connect to SFTP server"
                };
            }

            // Test if path exists (if provided)
            bool pathExists = false;
            string pathType = "Unknown";
            
            if (!string.IsNullOrEmpty(config.Path))
            {
                pathExists = sftpClient.Exists(config.Path);
                if (pathExists)
                {
                    var attrs = sftpClient.GetAttributes(config.Path);
                    pathType = attrs.IsDirectory ? "Directory" : "File";
                }
                else
                {
                    pathType = "Not Found";
                }
            }

            var details = new Dictionary<string, object>
            {
                ["Host"] = config.SftpHost,
                ["Port"] = port,
                ["IsConnected"] = sftpClient.IsConnected,
                ["ProtocolVersion"] = sftpClient.ProtocolVersion,
                ["ServerVersion"] = sftpClient.ConnectionInfo.ServerVersion,
                ["Path"] = config.Path ?? "Not specified",
                ["PathExists"] = pathExists,
                ["PathType"] = pathType
            };

            sftpClient.Disconnect();

            return new ConnectionTestResult
            {
                Success = true,
                Message = $"Successfully connected to SFTP server at {config.SftpHost}:{port}",
                Details = details
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SFTP connection test failed");
            
            // Extract the most relevant error message
            var errorMessage = GetRootErrorMessage(ex);
            
            return new ConnectionTestResult
            {
                Success = false,
                Message = $"SFTP connection failed: {errorMessage}"
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
                        string? token = null;
                        
                        // Check if dynamic token generation is enabled
                        if (apiConfig.UseDynamicToken)
                        {
                            var tokenResult = await GenerateDynamicTokenAsync(apiConfig);
                            if (!tokenResult.Success)
                            {
                                return new ConnectionTestResult
                                {
                                    Success = false,
                                    Message = $"Failed to generate dynamic token: {tokenResult.Message}"
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
                ["TestPath"] = testPath,
                ["UsedDynamicToken"] = apiConfig.UseDynamicToken
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

    private async Task<(bool Success, string? Token, string Message)> GenerateDynamicTokenAsync(ApiConfig apiConfig)
    {
        if (string.IsNullOrEmpty(apiConfig.TokenEndpointUrl))
        {
            return (false, null, "Token endpoint URL is required for dynamic token generation");
        }

        try
        {
            var tokenClient = _httpClientFactory.CreateClient();
            tokenClient.Timeout = TimeSpan.FromSeconds(30);
            
            // Set default headers
            tokenClient.DefaultRequestHeaders.Add("User-Agent", "MultiTenantETL/1.0");
            tokenClient.DefaultRequestHeaders.Add("Accept", "*/*");

            var request = new HttpRequestMessage
            {
                Method = apiConfig.TokenEndpointMethod?.ToUpper() == "GET" ? HttpMethod.Get : HttpMethod.Post,
                RequestUri = new Uri(apiConfig.TokenEndpointUrl)
            };

            // Add body for POST requests
            if (request.Method == HttpMethod.Post && !string.IsNullOrEmpty(apiConfig.TokenEndpointBody))
            {
                request.Content = new StringContent(
                    apiConfig.TokenEndpointBody,
                    System.Text.Encoding.UTF8,
                    "application/json");
            }

            // Add headers for token request (after content to avoid Content-Type conflicts)
            if (apiConfig.TokenEndpointHeaders != null)
            {
                foreach (var header in apiConfig.TokenEndpointHeaders)
                {
                    // Skip Content-Type as it's already set by StringContent
                    if (header.Key.Equals("Content-Type", StringComparison.OrdinalIgnoreCase))
                        continue;
                        
                    request.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }
            }

            var response = await tokenClient.SendAsync(request);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                return (false, null, $"Token endpoint returned {response.StatusCode}: {errorContent}");
            }

            var responseContent = await response.Content.ReadAsStringAsync();
            var tokenJson = JsonSerializer.Deserialize<JsonElement>(responseContent);

            // Extract token using the specified path (e.g., "access_token" or "data.token")
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
            _logger.LogError(ex, "Failed to generate dynamic token from {TokenEndpoint}", apiConfig.TokenEndpointUrl);
            return (false, null, $"Token generation failed: {ex.Message}");
        }
    }

    private static string? ExtractTokenFromResponse(JsonElement json, string path)
    {
        try
        {
            // Handle simple path (e.g., "access_token")
            if (!path.Contains('.'))
            {
                if (json.TryGetProperty(path, out var value))
                {
                    return value.GetString();
                }
                return null;
            }

            // Handle nested path (e.g., "data.token")
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
            return null;
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

    /// <summary>
    /// Extracts the root cause error message from an exception, avoiding repetitive nested messages
    /// </summary>
    private static string GetRootErrorMessage(Exception ex)
    {
        // Get the innermost exception
        var innermost = ex;
        while (innermost.InnerException != null)
        {
            innermost = innermost.InnerException;
        }

        // Return the innermost message, which is usually the most specific
        return innermost.Message;
    }
}
