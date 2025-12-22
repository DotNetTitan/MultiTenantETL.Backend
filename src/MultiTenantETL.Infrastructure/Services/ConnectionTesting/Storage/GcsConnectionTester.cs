using Google.Cloud.Storage.V1;
using Microsoft.Extensions.Logging;
using MultiTenantETL.Application.Connectors;
using MultiTenantETL.Application.Connectors.Models;

namespace MultiTenantETL.Infrastructure.Services.ConnectionTesting.Storage;

public class GcsConnectionTester
{
    private readonly ILogger<GcsConnectionTester> _logger;

    public GcsConnectionTester(ILogger<GcsConnectionTester> logger)
    {
        _logger = logger;
    }

    public async Task<ConnectionTestResult> TestConnectionAsync(FileConfig config)
    {
        if (string.IsNullOrEmpty(config.GcsProjectId))
        {
            return new ConnectionTestResult
            {
                Success = false,
                Message = "GCP Project ID is required"
            };
        }

        if (string.IsNullOrEmpty(config.GcsBucket))
        {
            return new ConnectionTestResult
            {
                Success = false,
                Message = "GCS Bucket name is required"
            };
        }

        if (string.IsNullOrEmpty(config.GcsJsonCredentials))
        {
            return new ConnectionTestResult
            {
                Success = false,
                Message = "GCS JSON Credentials are required"
            };
        }

        try
        {
            var credential = Google.Apis.Auth.OAuth2.GoogleCredential.FromJson(config.GcsJsonCredentials);
            using var storageClient = StorageClient.Create(credential);

            // Test connection by checking if bucket exists and is accessible
            var bucket = await storageClient.GetBucketAsync(config.GcsBucket);

            var details = new Dictionary<string, object>
            {
                ["Bucket"] = config.GcsBucket!,
                ["ProjectId"] = config.GcsProjectId!,
                ["Location"] = bucket.Location ?? string.Empty,
                ["StorageClass"] = bucket.StorageClass ?? string.Empty,
                ["ObjectKey"] = config.Path ?? string.Empty
            };

            return new ConnectionTestResult
            {
                Success = true,
                Message = $"Successfully connected to GCS bucket '{config.GcsBucket}' in project '{config.GcsProjectId}'",
                Details = details
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GCS connection test failed");
            
            var errorMessage = GetRootErrorMessage(ex);
            
            return new ConnectionTestResult
            {
                Success = false,
                Message = $"GCS connection failed: {errorMessage}"
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
