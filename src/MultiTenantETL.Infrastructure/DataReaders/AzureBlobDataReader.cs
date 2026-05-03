using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using MultiTenantETL.Application.Connectors.DataReaders;
using MultiTenantETL.Domain.Entities;
using MultiTenantETL.Infrastructure.Services.Storage;
using IDataReader = MultiTenantETL.Application.Connectors.DataReaders.IDataReader;

namespace MultiTenantETL.Infrastructure.DataReaders;

/// <summary>
/// Reads files from Azure Blob Storage.
/// Downloads stream and delegates to existing format-specific readers.
/// The Azure SDK returns a non-seekable RetriableStream that cannot be JSON-serialized
/// (its Length property throws NotSupportedException). To pass the live stream to the
/// sub-readers without going through JSON, we use a static registry keyed by a
/// temporary connector GUID that is cleaned up after the read completes.
/// </summary>
public class AzureBlobDataReader : IDataReader
{
    // Registry used to pass live Stream references to sub-readers without JSON serialization.
    // Entries are short-lived (created just before a read, removed immediately after).
    private static readonly ConcurrentDictionary<Guid, Stream> _streamRegistry = new();

    private readonly IStorageClientFactory _clientFactory;
    private readonly CsvDataReader _csvReader;
    private readonly JsonDataReader _jsonReader;
    private readonly JsonLinesDataReader _jsonLinesReader;
    private readonly ILogger<AzureBlobDataReader> _logger;

    public AzureBlobDataReader(
        IStorageClientFactory clientFactory,
        CsvDataReader csvReader,
        JsonDataReader jsonReader,
        JsonLinesDataReader jsonLinesReader,
        ILogger<AzureBlobDataReader> logger)
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

        var format = DetermineFormat(config.BlobName, config.Format);
        if (!IsSupportedFormat(format))
            throw new NotSupportedException($"File format '{format}' is not supported for Azure Blob");

        var containerClient = _clientFactory.CreateAzureBlobClient(config.AccountName, config.AccountKey, config.ContainerName);
        var blobClient = containerClient.GetBlobClient(config.BlobName);

        var response = await blobClient.DownloadStreamingAsync(cancellationToken: cancellationToken);
        var stream = response.Value.Content;

        var streamConnector = CreateStreamConnector(stream, format, out var registryKey);
        var reader = GetReaderForFormat(format);

        try
        {
            await foreach (var batch in reader.ReadAsync(streamConnector, options, cancellationToken))
            {
                yield return batch;
            }
        }
        finally
        {
            _streamRegistry.TryRemove(registryKey, out _);
        }
    }

    public async Task<bool> TestConnectionAsync(Connector connector, CancellationToken cancellationToken)
    {
        try
        {
            var config = ParseConfig(connector.ConfigJson);
            var containerClient = _clientFactory.CreateAzureBlobClient(config.AccountName, config.AccountKey, config.ContainerName);
            var blobClient = containerClient.GetBlobClient(config.BlobName);
            return await blobClient.ExistsAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Azure Blob connection test failed");
            return false;
        }
    }

    public async Task<SchemaDetectionResult> DetectSchemaAsync(Connector connector, CancellationToken cancellationToken)
    {
        try
        {
            var config = ParseConfig(connector.ConfigJson);
            var containerClient = _clientFactory.CreateAzureBlobClient(config.AccountName, config.AccountKey, config.ContainerName);
            var blobClient = containerClient.GetBlobClient(config.BlobName);

            var response = await blobClient.DownloadStreamingAsync(cancellationToken: cancellationToken);
            var stream = response.Value.Content;
            var format = DetermineFormat(config.BlobName, config.Format);

            var streamConnector = CreateStreamConnector(stream, format, out var registryKey);
            var reader = GetReaderForFormat(format);

            try
            {
                return await reader.DetectSchemaAsync(streamConnector, cancellationToken);
            }
            finally
            {
                _streamRegistry.TryRemove(registryKey, out _);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Schema detection failed for Azure Blob");
            return new SchemaDetectionResult
            {
                Success = false,
                ErrorMessage = ex.Message,
                DetectedAt = DateTimeOffset.UtcNow
            };
        }
    }

    // Returns a Connector whose ConfigJson encodes the registry key.
    // The sub-reader's ParseConfig will call GetStreamFromRegistry to retrieve the live stream.
    private static Connector CreateStreamConnector(Stream stream, string format, out Guid registryKey)
    {
        registryKey = Guid.NewGuid();
        _streamRegistry[registryKey] = stream;

        var configJson = format.ToLower() switch
        {
            "csv"  => JsonSerializer.Serialize(new { StreamRegistryKey = registryKey, HasHeader = true, Delimiter = "," }),
            "json" => JsonSerializer.Serialize(new { StreamRegistryKey = registryKey, IsArray = true }),
            _      => JsonSerializer.Serialize(new { StreamRegistryKey = registryKey })
        };

        return new Connector
        {
            Id = registryKey,
            TenantId = Guid.Empty,
            Name = "AzureBlobStreamConnector",
            Type = "File",
            Provider = "AzureBlob",
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

    /// <summary>
    /// Retrieves a stream that was previously registered via <see cref="CreateStreamConnector"/>.
    /// Sub-readers call this instead of deserializing a Stream from JSON.
    /// Returns null if the key is not found (e.g. normal file-based connectors).
    /// </summary>
    public static Stream? GetStreamFromRegistry(Guid key)
        => _streamRegistry.TryGetValue(key, out var stream) ? stream : null;

    /// <summary>Registers an externally obtained stream (e.g. from S3, GCS, FTP, SFTP).</summary>
    public static void RegisterStream(Guid key, Stream stream)
        => _streamRegistry[key] = stream;

    /// <summary>Removes a stream from the registry after use.</summary>
    public static void RemoveStreamFromRegistry(Guid key)
        => _streamRegistry.TryRemove(key, out _);

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

    private string DetermineFormat(string blobName, string? configFormat)
    {
        if (!string.IsNullOrEmpty(configFormat))
        {
            return configFormat;
        }

        var extension = Path.GetExtension(blobName).TrimStart('.').ToLower();
        return extension switch
        {
            "csv"  => "csv",
            "json" => "json",
            "jsonl" or "ndjson" => "jsonl",
            _ => "jsonl"
        };
    }

    private bool IsSupportedFormat(string format)
    {
        return format.ToLower() switch
        {
            "csv" or "json" or "jsonl" or "jsonlines" or "ndjson" => true,
            _ => false
        };
    }

    private AzureBlobConfig ParseConfig(string configJson)
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        var config = JsonSerializer.Deserialize<AzureBlobConfig>(configJson, options)
            ?? throw new InvalidOperationException("Invalid Azure Blob configuration");

        if (string.IsNullOrWhiteSpace(config.AccountName))
            throw new InvalidOperationException("AccountName is required");
        if (string.IsNullOrWhiteSpace(config.AccountKey))
            throw new InvalidOperationException("AccountKey is required");
        if (string.IsNullOrWhiteSpace(config.ContainerName))
            throw new InvalidOperationException("Azure Blob configuration must include ContainerName");
        if (string.IsNullOrWhiteSpace(config.BlobName))
            throw new InvalidOperationException("Azure Blob configuration must include BlobName");

        return config;
    }

    private class AzureBlobConfig
    {
        public string AccountName { get; set; } = string.Empty;
        public string AccountKey { get; set; } = string.Empty;
        public string ContainerName { get; set; } = string.Empty;
        public string BlobName { get; set; } = string.Empty;
        public string? Format { get; set; }
    }
}