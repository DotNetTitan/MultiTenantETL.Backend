using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Logging;
using MultiTenantETL.Application.Connectors;
using MultiTenantETL.Application.Connectors.Models;

namespace MultiTenantETL.Infrastructure.Services.ConnectionTesting.Storage;

public class S3ConnectionTester
{
    private readonly ILogger<S3ConnectionTester> _logger;

    public S3ConnectionTester(ILogger<S3ConnectionTester> logger)
    {
        _logger = logger;
    }

    public async Task<ConnectionTestResult> TestConnectionAsync(FileConfig config)
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
            var s3Config = new AmazonS3Config
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
            
            var errorMessage = GetRootErrorMessage(ex);
            
            return new ConnectionTestResult
            {
                Success = false,
                Message = $"S3 connection failed: {errorMessage}"
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
