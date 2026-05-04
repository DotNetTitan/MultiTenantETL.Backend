using Microsoft.Extensions.Logging;
using MultiTenantETL.Application.Connectors.DataReaders;
using MultiTenantETL.Application.Connectors.DataWriters;
using MultiTenantETL.Domain.Entities;
using Renci.SshNet;
using System.Text.Json;

namespace MultiTenantETL.Infrastructure.DataWriters;

/// <summary>
/// Writes data to SFTP servers
/// Buffers to memory stream and uploads on disposal
/// </summary>
public class SftpDataWriter : IDataWriter
{
    private readonly ILogger<SftpDataWriter> _logger;
    private MemoryStream? _bufferStream;
    private SftpConfig? _config;
    private string? _format;
    private bool _isFirstBatch = true;

    public SftpDataWriter(ILogger<SftpDataWriter> logger)
    {
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
                _format = DetermineFormat(_config.FilePath, _config.Format);
                _bufferStream = new MemoryStream();
            }

            if (_bufferStream == null)
            {
                throw new InvalidOperationException("Buffer stream not initialized");
            }

            if (_format == null)
            {
                throw new InvalidOperationException("Format not determined");
            }

            // Write batch to buffer stream based on format
            await WriteBatchToStreamAsync(_bufferStream, batch, _format, cancellationToken);

            result.RowsWritten = batch.RowCount;
            result.RowsFailed = 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write batch to SFTP buffer");
            result.RowsFailed = batch.RowCount;
            result.Errors.Add(ex.Message);
        }

        return result;
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

    private string DetermineFormat(string filePath, string? configFormat)
    {
        if (!string.IsNullOrEmpty(configFormat))
        {
            return configFormat;
        }

        var extension = Path.GetExtension(filePath).TrimStart('.').ToLower();
        return extension switch
        {
            "csv" => "csv",
            "json" => "json",
            "jsonl" or "ndjson" => "jsonl",
            _ => "jsonl"
        };
    }

    private SftpConfig ParseConfig(string configJson)
    {
        try
        {
            var config = JsonSerializer.Deserialize<SftpConfig>(configJson)
                ?? throw new InvalidOperationException("Invalid SFTP configuration");

            // Validate required fields
            if (string.IsNullOrEmpty(config.Host))
                throw new InvalidOperationException("SFTP host is required");
            if (string.IsNullOrEmpty(config.Username))
                throw new InvalidOperationException("SFTP username is required");
            if (string.IsNullOrEmpty(config.Password))
                throw new InvalidOperationException("SFTP password is required");
            if (string.IsNullOrEmpty(config.FilePath))
                throw new InvalidOperationException("SFTP file path is required");

            return config;
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException("Invalid SFTP configuration JSON", ex);
        }
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (_config != null && _bufferStream != null && _bufferStream.Length > 0)
            {
                _bufferStream.Position = 0;

                using var client = new SftpClient(_config.Host, _config.Port, _config.Username, _config.Password);
                client.Connect();

                // Upload the buffered file
                client.UploadFile(_bufferStream, _config.FilePath, true);

                client.Disconnect();

                _logger.LogInformation("Successfully uploaded file to SFTP: {FilePath}", _config.FilePath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during SFTP upload");
            throw;
        }
        finally
        {
            _bufferStream?.Dispose();
            _bufferStream = null;
        }
    }

    private class SftpConfig
    {
        public string Host { get; set; } = string.Empty;
        public int Port { get; set; } = 22;
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public string? Format { get; set; }
    }
}
