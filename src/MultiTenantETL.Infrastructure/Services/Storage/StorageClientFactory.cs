using Amazon.S3;
using Azure.Storage.Blobs;
using FluentFTP;
using Microsoft.Extensions.Logging;
using Renci.SshNet;

namespace MultiTenantETL.Infrastructure.Services.Storage;

public interface IStorageClientFactory
{
    AsyncFtpClient CreateFtpClient(string host, int port, string username, string password);
    SftpClient CreateSftpClient(string host, int port, string username, string password);
    AmazonS3Client CreateS3Client(string accessKey, string secretKey, string region, string? endpoint = null);
    BlobContainerClient CreateAzureBlobClient(string accountName, string accountKey, string containerName);
}

public class StorageClientFactory : IStorageClientFactory
{
    private readonly ILogger<StorageClientFactory> _logger;

    public StorageClientFactory(ILogger<StorageClientFactory> logger)
    {
        _logger = logger;
    }

    public AsyncFtpClient CreateFtpClient(string host, int port, string username, string password)
    {
        return new AsyncFtpClient(host, username, password, port);
    }

    public SftpClient CreateSftpClient(string host, int port, string username, string password)
    {
        return new SftpClient(host, port, username, password);
    }

    public AmazonS3Client CreateS3Client(string accessKey, string secretKey, string region, string? endpoint = null)
    {
        var s3Config = new AmazonS3Config
        {
            MaxErrorRetry = 3,
            Timeout = TimeSpan.FromSeconds(30),
            ForcePathStyle = !string.IsNullOrEmpty(endpoint)
        };

        if (!string.IsNullOrEmpty(endpoint))
        {
            s3Config.ServiceURL = endpoint;
        }
        else
        {
            s3Config.RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(region);
        }

        return new AmazonS3Client(accessKey, secretKey, s3Config);
    }

    public BlobContainerClient CreateAzureBlobClient(string accountName, string accountKey, string containerName)
    {
        var connectionString = $"DefaultEndpointsProtocol=https;AccountName={accountName};AccountKey={accountKey};EndpointSuffix=core.windows.net";

        var blobClientOptions = new BlobClientOptions
        {
            Retry = {
                MaxRetries = 3,
                Delay = TimeSpan.FromSeconds(1),
                MaxDelay = TimeSpan.FromSeconds(3),
                Mode = Azure.Core.RetryMode.Fixed
            }
        };

        var blobServiceClient = new BlobServiceClient(connectionString, blobClientOptions);
        return blobServiceClient.GetBlobContainerClient(containerName);
    }
}
