using Microsoft.Extensions.Logging;
using MultiTenantETL.Application.Connectors;
using MultiTenantETL.Application.Connectors.Models;
using MultiTenantETL.Infrastructure.Configuration;
using System.Text.Json;

namespace MultiTenantETL.Infrastructure.Services.ConnectionTesting.Storage;

public class StorageConnectionTester : IStorageConnectionTester
{
    private readonly AzureBlobConnectionTester _azureBlobTester;
    private readonly FtpConnectionTester _ftpTester;
    private readonly SftpConnectionTester _sftpTester;
    private readonly ILogger<StorageConnectionTester> _logger;

    public StorageConnectionTester(
        AzureBlobConnectionTester azureBlobTester,
        FtpConnectionTester ftpTester,
        SftpConnectionTester sftpTester,
        ILogger<StorageConnectionTester> logger)
    {
        _azureBlobTester = azureBlobTester;
        _ftpTester = ftpTester;
        _sftpTester = sftpTester;
        _logger = logger;
    }

    public async Task<ConnectionTestResult> TestConnectionAsync(string provider, JsonElement config)
    {
        var fileConfig = JsonSerializer.Deserialize<FileConfig>(config, JsonSerializerOptionsProvider.Default);
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
