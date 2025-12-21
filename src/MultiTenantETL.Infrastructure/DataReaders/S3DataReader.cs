using System.Runtime.CompilerServices;
using System.Text.Json;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Logging;
using MultiTenantETL.Application.Connectors.DataReaders;
using MultiTenantETL.Domain.Entities;
using MultiTenantETL.Infrastructure.Services.Storage;
using IDataReader = MultiTenantETL.Application.Connectors.DataReaders.IDataReader;

namespace MultiTenantETL.Infrastructure.DataReaders;

/// <summary>
/// Reads files from AWS S3 (or S3-compatible storage like MinIO)
/// Downloads stream from S3 and delegates to existing format-specific readers
/// </summary>
public class S3DataReader : IDataReader
{
    private readonly IStorageClientFactory _clientFactory;
    private readonly CsvDataReader _csvReader;
    private readonly JsonDataReader _jsonReader;
    private readonly JsonLinesDataReader _jsonLinesReader;
    private readonly ILogger<S3DataReader> _logger;

    public S3DataReader(
        IStorageClientFactory clientFactory,
        CsvDataReader csvReader,
        JsonDataReader jsonReader,
        JsonLinesDataReader jsonLinesReader,
        ILogger<S3DataReader> logger)
    {
        _clientFactory = clientFactory ?? throw new ArgumentNullException(nameof(clientFactory));
        _csvReader = csvReader ?? throw new ArgumentNullException(nameof(csvReader));
        _jsonReader = jsonReader ?? throw new ArgumentNullException(nameof(jsonReader));
        _jsonLinesReader = jsonLinesReader ?? throw new ArgumentNullException(nameof(jsonLinesReader));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async IAsyncEnumerable<ReadBatch> ReadAsync(
        Connector connector,
        ReadOptions options,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var config = ParseConfig(connector.ConfigJson);

        if (string.IsNullOrEmpty(config.Bucket))
            throw new InvalidOperationException("S3 configuration must include BucketName");

        if (string.IsNullOrEmpty(config.Key))
            throw new InvalidOperationException("S3 configuration must include Key");

        if (string.IsNullOrEmpty(config.AccessKey))
            throw new InvalidOperationException("S3 configuration must include AccessKey");

        if (string.IsNullOrEmpty(config.SecretKey))
            throw new InvalidOperationException("S3 configuration must include SecretKey");

        if (string.IsNullOrEmpty(config.Region))
            throw new InvalidOperationException("S3 configuration must include Region");

        var format = DetermineFormat(config.Key, config.Format);
        if (!IsSupportedFormat(format))
            throw new NotSupportedException($"File format '{format}' is not supported for S3");

        using IAmazonS3 s3Client = _clientFactory.CreateS3Client(config.AccessKey, config.SecretKey, config.Region, config.Endpoint);

        var request = new GetObjectRequest
        {
            BucketName = config.Bucket,
            Key = config.Key
        };

        using var response = await s3Client.GetObjectAsync(request, cancellationToken);

        // Pass stream directly to format-specific reader
        var streamConnector = CreateStreamConnector(response.ResponseStream, format);
        var reader = GetReaderForFormat(format);

        await foreach (var batch in reader.ReadAsync(streamConnector, options, cancellationToken))
        {
            yield return batch;
        }
    }

    private IDataReader GetReaderForFormat(string format)
    {
        return format.ToLower() switch
        {
            "csv" => _csvReader,
            "json" => _jsonReader,
            "jsonl" or "jsonlines" or "ndjson" => _jsonLinesReader,
            _ => throw new NotSupportedException($"File format '{format}' is not supported")
        };
    }

    private Connector CreateStreamConnector(Stream stream, string format)
    {
        var configJson = format.ToLower() switch
        {
            "csv" => JsonSerializer.Serialize(new { Stream = stream, HasHeader = true, Delimiter = "," }),
            "json" => JsonSerializer.Serialize(new { Stream = stream, IsArray = true }),
            "jsonl" or "jsonlines" or "ndjson" => JsonSerializer.Serialize(new { Stream = stream }),
            _ => JsonSerializer.Serialize(new { Stream = stream })
        };

        return new Connector
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.Empty,
            Name = "S3StreamConnector",
            Type = "File",
            Provider = "S3",
            Direction = "source",
            IsSource = true,
            IsDestination = false,
            RequiresCredentials = false,
            IsActive = true,
            ConfigJson = configJson,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = Guid.Empty
        };
    }

    public async Task<bool> TestConnectionAsync(Connector connector, CancellationToken cancellationToken)
    {
        try
        {
            var config = ParseConfig(connector.ConfigJson);
            using IAmazonS3 s3Client = _clientFactory.CreateS3Client(config.AccessKey, config.SecretKey, config.Region, config.Endpoint);

            var request = new GetBucketLocationRequest
            {
                BucketName = config.Bucket
            };

            await s3Client.GetBucketLocationAsync(request, cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "S3 connection test failed");
            return false;
        }
    }

    public async Task<SchemaDetectionResult> DetectSchemaAsync(Connector connector, CancellationToken cancellationToken)
    {
        try
        {
            var config = ParseConfig(connector.ConfigJson);
            using IAmazonS3 s3Client = _clientFactory.CreateS3Client(config.AccessKey, config.SecretKey, config.Region, config.Endpoint);

            var request = new GetObjectRequest
            {
                BucketName = config.Bucket,
                Key = config.Key
            };

            using var response = await s3Client.GetObjectAsync(request, cancellationToken);
            var format = DetermineFormat(config.Key, config.Format);

            var streamConnector = CreateStreamConnector(response.ResponseStream, format);
            var reader = GetReaderForFormat(format);

            return await reader.DetectSchemaAsync(streamConnector, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Schema detection failed for S3");
            return new SchemaDetectionResult
            {
                Success = false,
                ErrorMessage = ex.Message,
                DetectedAt = DateTimeOffset.UtcNow
            };
        }
    }

    private string DetermineFormat(string key, string? configFormat)
    {
        if (!string.IsNullOrEmpty(configFormat))
        {
            return configFormat;
        }

        var extension = Path.GetExtension(key).TrimStart('.').ToLower();
        return extension switch
        {
            "csv" => "csv",
            "json" => "json",
            "jsonl" or "ndjson" => "jsonl",
            _ => "jsonl" // Default to JSONL for streaming
        };
    }

    private bool IsSupportedFormat(string format)
    {
        return format switch
        {
            "csv" or "json" or "jsonl" or "jsonlines" or "ndjson" => true,
            _ => false
        };
    }

    private S3Config ParseConfig(string configJson)
    {
        return JsonSerializer.Deserialize<S3Config>(configJson)
            ?? throw new InvalidOperationException("Invalid S3 configuration");
    }

    private class S3Config
    {
        public string AccessKey { get; set; } = string.Empty;
        public string SecretKey { get; set; } = string.Empty;
        public string Region { get; set; } = string.Empty;
        public string Bucket { get; set; } = string.Empty;
        public string Key { get; set; } = string.Empty;
        public string? Endpoint { get; set; }
        public string? Format { get; set; }
    }
}
