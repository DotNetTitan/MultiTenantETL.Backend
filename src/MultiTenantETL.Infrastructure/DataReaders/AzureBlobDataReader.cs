using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using MultiTenantETL.Application.Connectors.DataReaders;
using MultiTenantETL.Domain.Entities;
using MultiTenantETL.Infrastructure.Services.Storage;
using IDataReader = MultiTenantETL.Application.Connectors.DataReaders.IDataReader;

namespace MultiTenantETL.Infrastructure.DataReaders;

/// <summary>
/// Reads files from Azure Blob Storage
/// Downloads stream and delegates to existing format-specific readers
/// </summary>
public class AzureBlobDataReader : IDataReader
{
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

        if (string.IsNullOrEmpty(config.ContainerName))
            throw new InvalidOperationException("Azure Blob configuration must include ContainerName");

        if (string.IsNullOrEmpty(config.BlobName))
            throw new InvalidOperationException("Azure Blob configuration must include BlobName");

        if (string.IsNullOrEmpty(config.AccountName))
            throw new InvalidOperationException("Azure Blob configuration must include AccountName");

        if (string.IsNullOrEmpty(config.AccountKey))
            throw new InvalidOperationException("Azure Blob configuration must include AccountKey");

        var format = DetermineFormat(config.BlobName, config.Format);
        if (!IsSupportedFormat(format))
            throw new NotSupportedException($"File format '{format}' is not supported for Azure Blob");

        var containerClient = _clientFactory.CreateAzureBlobClient(config.AccountName, config.AccountKey, config.ContainerName);
        var blobClient = containerClient.GetBlobClient(config.BlobName);

        var response = await blobClient.DownloadStreamingAsync(cancellationToken: cancellationToken);

        var streamConnector = CreateStreamConnector(response.Value.Content, format);
        var reader = GetReaderForFormat(format);

        await foreach (var batch in reader.ReadAsync(streamConnector, options, cancellationToken))
        {
            yield return batch;
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
            var format = DetermineFormat(config.BlobName, config.Format);

            var streamConnector = CreateStreamConnector(response.Value.Content, format);
            var reader = GetReaderForFormat(format);

            return await reader.DetectSchemaAsync(streamConnector, cancellationToken);
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

    private string DetermineFormat(string blobName, string? configFormat)
    {
        if (!string.IsNullOrEmpty(configFormat))
        {
            return configFormat;
        }

        var extension = Path.GetExtension(blobName).TrimStart('.').ToLower();
        return extension switch
        {
            "csv" => "csv",
            "json" => "json",
            "jsonl" or "ndjson" => "jsonl",
            _ => "jsonl"
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

    private AzureBlobConfig ParseConfig(string configJson)
    {
        return JsonSerializer.Deserialize<AzureBlobConfig>(configJson)
            ?? throw new InvalidOperationException("Invalid Azure Blob configuration");
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
