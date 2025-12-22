using System.Text.Json;
using Google.Cloud.Storage.V1;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MultiTenantETL.Application.Connectors.DataReaders;
using MultiTenantETL.Application.Connectors.DataWriters;
using MultiTenantETL.Domain.Entities;
using MultiTenantETL.Infrastructure.Configuration;
using MultiTenantETL.Infrastructure.Services.Storage;

namespace MultiTenantETL.Infrastructure.DataWriters;

/// <summary>
/// Writes data to Google Cloud Storage
/// </summary>
public class GcsDataWriter : IDataWriter
{
    private readonly IStorageClientFactory _clientFactory;
    private readonly ILogger<GcsDataWriter> _logger;
    private GcsConfig? _config;
    private string? _format;
    private MemoryStream? _uploadStream;
    private bool _isFirstBatch = true;

    public GcsDataWriter(
        IStorageClientFactory clientFactory,
        ILogger<GcsDataWriter> logger)
    {
        _clientFactory = clientFactory ?? throw new ArgumentNullException(nameof(clientFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<DataWriteResult> WriteBatchAsync(
        Connector connector,
        ReadBatch batch,
        WriteOptions options,
        CancellationToken cancellationToken)
    {
        var result = new DataWriteResult { BatchId = batch.BatchId };

        try
        {
            if (_config == null)
            {
                _config = ParseConfig(connector.ConfigJson);
                _format = DetermineFormat(_config.Key, _config.Format);
                _uploadStream = new MemoryStream();
            }

            if (_uploadStream == null)
            {
                throw new InvalidOperationException("Upload stream not initialized");
            }

            if (_format == null)
            {
                throw new InvalidOperationException("Format not determined");
            }

            // Write batch to the memory stream
            await WriteBatchToStreamAsync(_uploadStream, batch, _format, cancellationToken);

            result.RowsWritten = batch.RowCount;
            result.RowsFailed = 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write batch to GCS buffer");
            result.RowsFailed = batch.RowCount;
            result.Errors.Add(ex.Message);
        }

        return result;
    }

    private async Task WriteBatchToStreamAsync(Stream stream, ReadBatch batch, string format, CancellationToken cancellationToken)
    {
        // Keep the stream open for multiple batches
        var writer = new StreamWriter(stream, leaveOpen: true);

        switch (format.ToLower())
        {
            case "csv":
                await WriteCsvBatchAsync(writer, batch, cancellationToken);
                break;
            case "jsonl":
            case "jsonlines":
            case "ndjson":
            case "json": // Defaulting JSON to JSONL for streaming ease unless full rebuild
                await WriteJsonLinesBatchAsync(writer, batch, cancellationToken);
                break;
            default:
                await WriteJsonLinesBatchAsync(writer, batch, cancellationToken);
                break;
        }

        await writer.FlushAsync();
    }

    private async Task WriteCsvBatchAsync(StreamWriter writer, ReadBatch batch, CancellationToken cancellationToken)
    {
        if (batch.Rows.Count == 0) return;

        var headers = batch.Rows[0].Keys.ToList();

        if (_isFirstBatch)
        {
            await writer.WriteLineAsync(string.Join(",", headers.Select(h => EscapeCsvField(h))));
            _isFirstBatch = false;
        }

        foreach (var row in batch.Rows)
        {
            var values = headers.Select(h => EscapeCsvField(row[h]?.ToString() ?? ""));
            await writer.WriteLineAsync(string.Join(",", values));
        }
    }

    private async Task WriteJsonLinesBatchAsync(StreamWriter writer, ReadBatch batch, CancellationToken cancellationToken)
    {
        foreach (var row in batch.Rows)
        {
            var json = JsonSerializer.Serialize(row);
            await writer.WriteLineAsync(json);
        }
    }

    private string EscapeCsvField(string field)
    {
        if (field.Contains(',') || field.Contains('"') || field.Contains('\n'))
        {
            return $"\"{field.Replace("\"", "\"\"")}\"";
        }
        return field;
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
            _ => "jsonl"
        };
    }

    private GcsConfig ParseConfig(string configJson)
    {
        var config = JsonSerializer.Deserialize<GcsConfig>(configJson);
        if (config == null) throw new InvalidOperationException("Invalid GCS configuration");
        return config;
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (_uploadStream != null && _config != null)
            {
                _uploadStream.Position = 0;
                using var storageClient = _clientFactory.CreateGcsClient(_config.ProjectId, _config.JsonCredentials);
                
                var contentType = _format?.ToLower() switch
                {
                    "csv" => "text/csv",
                    "json" => "application/json",
                    _ => "application/x-ndjson"
                };

                await storageClient.UploadObjectAsync(_config.Bucket, _config.Key, contentType, _uploadStream);
                
                await _uploadStream.DisposeAsync();
                _uploadStream = null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during GCSDataWriter disposal/upload");
            throw;
        }
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
