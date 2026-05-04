using Azure.Storage;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Logging;
using MultiTenantETL.Application.Connectors;
using MultiTenantETL.Application.Connectors.Models;

namespace MultiTenantETL.Infrastructure.Services.ConnectionTesting.Storage;

public class AzureBlobConnectionTester
{
    private readonly ILogger<AzureBlobConnectionTester> _logger;

    public AzureBlobConnectionTester(ILogger<AzureBlobConnectionTester> logger)
    {
        _logger = logger;
    }

    public async Task<ConnectionTestResult> TestConnectionAsync(FileConfig config)
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
            // Create credentials securely
            var credential = new StorageSharedKeyCredential(config.AzureAccountName, config.AzureAccountKey);
            var serviceUri = new Uri($"https://{config.AzureAccountName}.blob.core.windows.net");

            // Configure retry options - reduce from default 6 to 3 attempts
            var blobClientOptions = new BlobClientOptions
            {
                Retry = {
                    MaxRetries = 3,
                    Delay = TimeSpan.FromSeconds(1),
                    MaxDelay = TimeSpan.FromSeconds(3),
                    Mode = Azure.Core.RetryMode.Fixed
                }
            };

            // Create blob service client with custom retry policy
            var blobServiceClient = new BlobServiceClient(serviceUri, credential, blobClientOptions);

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
                ["AccountName"] = config.AzureAccountName ?? string.Empty,
                ["Container"] = config.AzureContainer ?? string.Empty,
                ["BlobPath"] = config.Path ?? string.Empty,
                ["LastModified"] = properties.Value.LastModified!,
                ["HasImmutabilityPolicy"] = properties.Value.HasImmutabilityPolicy!
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
        catch (FormatException)
        {
            return new ConnectionTestResult
            {
                Success = false,
                Message = "Invalid Azure Storage Account Key format (must be Base64) or Account Name."
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Azure Blob connection test failed");

            var errorMessage = GetRootErrorMessage(ex);

            return new ConnectionTestResult
            {
                Success = false,
                Message = $"Azure Blob connection failed: {errorMessage}"
            };
        }
    }

    private static string GetRootErrorMessage(Exception ex)
    {
        var innermost = ex;
        while (innermost.InnerException != null)
        {
            innermost = innermost.InnerException;
        }
        return innermost.Message;
    }
}
