using FluentFTP;
using Microsoft.Extensions.Logging;
using MultiTenantETL.Application.Connectors.DataReaders;
using MultiTenantETL.Application.Connectors.DataWriters;
using MultiTenantETL.Domain.Entities;
using MultiTenantETL.Infrastructure.Security;
using System.Text.Json;

namespace MultiTenantETL.Infrastructure.DataWriters;

/// <summary>
/// Writes data to FTP servers
/// Buffers to memory stream and uploads on disposal
/// </summary>
public class FtpDataWriter : IDataWriter
{
    private readonly ISsrfGuard _ssrfGuard;
    private readonly ILogger<FtpDataWriter> _logger;
    private MemoryStream? _bufferStream;
    private FtpConfig? _config;
    private string? _format;
    private bool _isFirstBatch = true;

    public FtpDataWriter(ILogger<FtpDataWriter> logger, ISsrfGuard ssrfGuard)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _ssrfGuard = ssrfGuard;
    }

    public async Task<DataWriteResult> WriteBatchAsync(
        Connector connector,
        ReadBatch batch,
        WriteOptions options,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var result = new DataWriteResult { BatchId = batch.BatchId };

        // Parse and validate config upfront
        var config = ParseConfig(connector.ConfigJson);

        try
        {
            if (_config == null)
            {
                _config = config;
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
            _logger.LogError(ex, "Failed to write batch to FTP buffer");
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

    private FtpConfig ParseConfig(string configJson)
    {
        try
        {
            var config = JsonSerializer.Deserialize<FtpConfig>(configJson)
                ?? throw new InvalidOperationException("Invalid FTP configuration");

            if (!string.IsNullOrWhiteSpace(config.Host))
            {
                _ssrfGuard.ValidateHost(config.Host);
            }

            return config;
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException("Failed to parse FTP connector configuration", ex);
        }
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (_config != null && _bufferStream != null && _bufferStream.Length > 0)
            {
                _bufferStream.Position = 0;

                using var client = new AsyncFtpClient(_config.Host, _config.Username, _config.Password, _config.Port);
                await client.Connect();

                // Upload the buffered file
                await client.UploadStream(_bufferStream, _config.FilePath, FtpRemoteExists.Overwrite);

                await client.Disconnect();

                _logger.LogInformation("Successfully uploaded file to FTP: {FilePath}", _config.FilePath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during FTP upload");
            throw;
        }
        finally
        {
            _bufferStream?.Dispose();
            _bufferStream = null;
        }
    }

    private class FtpConfig
    {
        public string Host { get; set; } = string.Empty;
        public int Port { get; set; } = 21;
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public string? Format { get; set; }
    }
}
