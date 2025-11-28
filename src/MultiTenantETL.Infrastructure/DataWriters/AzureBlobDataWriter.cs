using System.Text.Json;
using Microsoft.Extensions.Logging;
using MultiTenantETL.Application.Connectors.DataReaders;
using MultiTenantETL.Application.Connectors.DataWriters;
using MultiTenantETL.Domain.Entities;
using MultiTenantETL.Infrastructure.Services.Storage;

namespace MultiTenantETL.Infrastructure.DataWriters;

/// <summary>
/// Writes data to Azure Blob Storage
/// Uses streaming upload to avoid memory issues with large files
/// </summary>
public class AzureBlobDataWriter : IDataWriter
{
    private readonly IStorageClientFactory _clientFactory;
    private readonly ILogger<AzureBlobDataWriter> _logger;
    private AzureBlobConfig? _config;
    private string? _format;
    private Stream? _uploadStream;
    private Task? _uploadTask;
    private bool _isFirstBatch = true;

    public AzureBlobDataWriter(
        IStorageClientFactory clientFactory,
        ILogger<AzureBlobDataWriter> logger)
    {
        _clientFactory = clientFactory;
        _logger = logger;
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
                _format = DetermineFormat(_config.BlobName, _config.Format);
                
                // Start streaming upload
                await InitializeUploadStreamAsync(cancellationToken);
            }

            if (_uploadStream == null)
            {
                throw new InvalidOperationException("Upload stream not initialized");
            }

            // Write batch directly to upload stream based on format
            await WriteBatchToStreamAsync(_uploadStream, batch, _format, cancellationToken);

            result.RowsWritten = batch.RowCount;
            result.RowsFailed = 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write batch to Azure Blob");
            result.RowsFailed = batch.RowCount;
            result.Errors.Add(ex.Message);
        }

        return result;
    }

    private async Task InitializeUploadStreamAsync(CancellationToken cancellationToken)
    {
        if (_config == null) return;

        var containerClient = _clientFactory.CreateAzureBlobClient(_config.AccountName, _config.AccountKey, _config.ContainerName);
        var blobClient = containerClient.GetBlobClient(_config.BlobName);

        // Create a pipe for streaming upload
        var pipe = new System.IO.Pipelines.Pipe();
        _uploadStream = pipe.Writer.AsStream();

        // Start upload task in background
        _uploadTask = Task.Run(async () =>
        {
            try
            {
                await blobClient.UploadAsync(pipe.Reader.AsStream(), overwrite: true, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Azure Blob upload failed");
                throw;
            }
        }, cancellationToken);
    }

    private async Task WriteBatchToStreamAsync(Stream stream, ReadBatch batch, string format, CancellationToken cancellationToken)
    {
        await using var writer = new StreamWriter(stream, leaveOpen: true);

        switch (format.ToLower())
        {
            case "csv":
                await WriteCsvBatchAsync(writer, batch, cancellationToken);
                break;
            case "jsonl":
            case "jsonlines":
            case "ndjson":
                await WriteJsonLinesBatchAsync(writer, batch, cancellationToken);
                break;
            case "json":
                _logger.LogWarning("JSON array format not recommended for streaming. Consider using JSONL.");
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

        // Write header only for first batch
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
            _ => "jsonl" // Default to JSONL for streaming
        };
    }

    private AzureBlobConfig ParseConfig(string configJson)
    {
        return JsonSerializer.Deserialize<AzureBlobConfig>(configJson)
            ?? throw new InvalidOperationException("Invalid Azure Blob configuration");
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            // Close the upload stream to signal completion
            if (_uploadStream != null)
            {
                await _uploadStream.FlushAsync();
                await _uploadStream.DisposeAsync();
                _uploadStream = null;
            }

            // Wait for upload to complete
            if (_uploadTask != null)
            {
                await _uploadTask;
                _uploadTask = null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during AzureBlobDataWriter disposal");
            throw;
        }
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
