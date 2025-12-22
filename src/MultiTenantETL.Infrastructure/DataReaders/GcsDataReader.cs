using System.Runtime.CompilerServices;
using System.Text.Json;
using Google.Cloud.Storage.V1;
using Microsoft.Extensions.Logging;
using MultiTenantETL.Application.Connectors.DataReaders;
using MultiTenantETL.Domain.Entities;
using MultiTenantETL.Infrastructure.Services.Storage;
using IDataReader = MultiTenantETL.Application.Connectors.DataReaders.IDataReader;

namespace MultiTenantETL.Infrastructure.DataReaders;

/// <summary>
/// Reads files from Google Cloud Storage
/// Downloads stream from GCS and delegates to existing format-specific readers
/// </summary>
public class GcsDataReader : IDataReader
{
    private readonly IStorageClientFactory _clientFactory;
    private readonly CsvDataReader _csvReader;
    private readonly JsonDataReader _jsonReader;
    private readonly JsonLinesDataReader _jsonLinesReader;
    private readonly ILogger<GcsDataReader> _logger;

    public GcsDataReader(
        IStorageClientFactory clientFactory,
        CsvDataReader csvReader,
        JsonDataReader jsonReader,
        JsonLinesDataReader jsonLinesReader,
        ILogger<GcsDataReader> logger)
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
            throw new InvalidOperationException("GCS configuration must include BucketName");

        if (string.IsNullOrEmpty(config.Key))
            throw new InvalidOperationException("GCS configuration must include Object Key (Path)");

        if (string.IsNullOrEmpty(config.ProjectId))
            throw new InvalidOperationException("GCS configuration must include ProjectId");

        if (string.IsNullOrEmpty(config.JsonCredentials))
            throw new InvalidOperationException("GCS configuration must include JsonCredentials");

        var format = DetermineFormat(config.Key, config.Format);
        if (!IsSupportedFormat(format))
            throw new NotSupportedException($"File format '{format}' is not supported for GCS");

        using var storageClient = _clientFactory.CreateGcsClient(config.ProjectId, config.JsonCredentials);

        using var stream = new MemoryStream();
        await storageClient.DownloadObjectAsync(config.Bucket, config.Key, stream, cancellationToken: cancellationToken);
        stream.Position = 0;

        // Pass stream directly to format-specific reader
        var streamConnector = CreateStreamConnector(stream, format);
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
            Name = "GcsStreamConnector",
            Type = "File",
            Provider = "GCS",
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
            using var storageClient = _clientFactory.CreateGcsClient(config.ProjectId, config.JsonCredentials);

            await storageClient.GetBucketAsync(config.Bucket, cancellationToken: cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GCS connection test failed");
            return false;
        }
    }

    public async Task<SchemaDetectionResult> DetectSchemaAsync(Connector connector, CancellationToken cancellationToken)
    {
        try
        {
            var config = ParseConfig(connector.ConfigJson);
            using var storageClient = _clientFactory.CreateGcsClient(config.ProjectId, config.JsonCredentials);

            using var stream = new MemoryStream();
            await storageClient.DownloadObjectAsync(config.Bucket, config.Key, stream, cancellationToken: cancellationToken);
            stream.Position = 0;

            var format = DetermineFormat(config.Key, config.Format);
            var streamConnector = CreateStreamConnector(stream, format);
            var reader = GetReaderForFormat(format);

            return await reader.DetectSchemaAsync(streamConnector, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Schema detection failed for GCS");
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
            _ => "jsonl" // Default to JSONL
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

    private GcsConfig ParseConfig(string configJson)
    {
        var config = JsonSerializer.Deserialize<GcsConfig>(configJson);
        if (config == null) throw new InvalidOperationException("Invalid GCS configuration");

        // Map DTO fields to internal config if needed (ConfigJson might use GcsBucket vs Bucket)
        // But for simplicity in internal parsing, we follow the pattern in S3DataReader
        return config;
    }

    private class GcsConfig
    {
        public string ProjectId { get; set; } = string.Empty;
        public string JsonCredentials { get; set; } = string.Empty;
        public string Bucket { get; set; } = string.Empty;
        public string Key { get; set; } = string.Empty;
        public string? Format { get; set; }
    }
}
