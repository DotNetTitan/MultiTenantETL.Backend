using System.Text.Json;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Logging;
using MultiTenantETL.Application.Connectors.DataReaders;
using MultiTenantETL.Application.Connectors.DataWriters;
using MultiTenantETL.Domain.Entities;
using MultiTenantETL.Infrastructure.Services.Storage;

namespace MultiTenantETL.Infrastructure.DataWriters;

/// <summary>
/// Writes data to AWS S3 (or S3-compatible storage like MinIO)
/// Uses streaming upload to avoid memory issues with large files
/// </summary>
public class S3DataWriter : IDataWriter
{
    private readonly IStorageClientFactory _clientFactory;
    private readonly ILogger<S3DataWriter> _logger;
    private S3Config? _config;
    private string? _format;
    private Stream? _uploadStream;
    private Task? _uploadTask;
    private bool _isFirstBatch = true;

    public S3DataWriter(
        IStorageClientFactory clientFactory,
        ILogger<S3DataWriter> logger)
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
                
                // Start multipart upload stream
                await InitializeUploadStreamAsync(cancellationToken);
            }

            if (_uploadStream == null)
            {
                throw new InvalidOperationException("Upload stream not initialized");
            }

            if (_format == null)
            {
                throw new InvalidOperationException("Format not determined");
            }

            // Write batch directly to upload stream based on format
            await WriteBatchToStreamAsync(_uploadStream, batch, _format, cancellationToken);

            result.RowsWritten = batch.RowCount;
            result.RowsFailed = 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write batch to S3");
            result.RowsFailed = batch.RowCount;
            result.Errors.Add(ex.Message);
        }

        return result;
    }

    private async Task InitializeUploadStreamAsync(CancellationToken cancellationToken)
    {
        if (_config == null) return;

        IAmazonS3 s3Client = _clientFactory.CreateS3Client(_config.AccessKey, _config.SecretKey, _config.Region, _config.Endpoint);
        
        var contentType = _format?.ToLower() switch
        {
            "csv" => "text/csv",
            "json" => "application/json",
            _ => "application/x-ndjson"
        };

        // Create a pipe for streaming upload
        var pipe = new System.IO.Pipelines.Pipe();
        _uploadStream = pipe.Writer.AsStream();

        // Start upload task in background
        _uploadTask = Task.Run(async () =>
        {
            try
            {
                var request = new PutObjectRequest
                {
                    BucketName = _config.Bucket,
                    Key = _config.Key,
                    InputStream = pipe.Reader.AsStream(),
                    ContentType = contentType
                };

                await s3Client.PutObjectAsync(request, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "S3 upload failed");
                throw;
            }
            finally
            {
                s3Client.Dispose();
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

    private S3Config ParseConfig(string configJson)
    {
        try
        {
            var config = JsonSerializer.Deserialize<S3Config>(configJson)
                ?? throw new InvalidOperationException("Invalid S3 configuration");

            // Validate required fields
            if (string.IsNullOrEmpty(config.AccessKey))
                throw new InvalidOperationException("S3 access key is required");
            if (string.IsNullOrEmpty(config.SecretKey))
                throw new InvalidOperationException("S3 secret key is required");
            if (string.IsNullOrEmpty(config.Region))
                throw new InvalidOperationException("S3 region is required");
            if (string.IsNullOrEmpty(config.Bucket))
                throw new InvalidOperationException("S3 bucket is required");
            if (string.IsNullOrEmpty(config.Key))
                throw new InvalidOperationException("S3 key is required");

            return config;
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException("Invalid S3 configuration JSON", ex);
        }
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
            _logger.LogError(ex, "Error during S3DataWriter disposal");
            throw;
        }
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
