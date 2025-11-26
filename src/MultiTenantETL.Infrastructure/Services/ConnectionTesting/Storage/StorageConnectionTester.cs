using System.Text.Json;
using Microsoft.Extensions.Logging;
using MultiTenantETL.Application.Connectors;
using MultiTenantETL.Application.Connectors.Models;

namespace MultiTenantETL.Infrastructure.Services.ConnectionTesting.Storage;

public class StorageConnectionTester : IStorageConnectionTester
{
    private readonly AzureBlobConnectionTester _azureBlobTester;
    private readonly S3ConnectionTester _s3Tester;
    private readonly FtpConnectionTester _ftpTester;
    private readonly SftpConnectionTester _sftpTester;
    private readonly ILogger<StorageConnectionTester> _logger;
    
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public StorageConnectionTester(
        AzureBlobConnectionTester azureBlobTester,
        S3ConnectionTester s3Tester,
        FtpConnectionTester ftpTester,
        SftpConnectionTester sftpTester,
        ILogger<StorageConnectionTester> logger)
    {
        _azureBlobTester = azureBlobTester;
        _s3Tester = s3Tester;
        _ftpTester = ftpTester;
        _sftpTester = sftpTester;
        _logger = logger;
    }

    public async Task<ConnectionTestResult> TestConnectionAsync(string provider, JsonElement config)
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
                "Local" => LocalFileConnectionTester.TestConnection(fileConfig),
                "FTP" => await _ftpTester.TestConnectionAsync(fileConfig),
                "SFTP" => await _sftpTester.TestConnectionAsync(fileConfig),
                "S3" => await _s3Tester.TestConnectionAsync(fileConfig),
                "AzureBlob" => await _azureBlobTester.TestConnectionAsync(fileConfig),
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
}
