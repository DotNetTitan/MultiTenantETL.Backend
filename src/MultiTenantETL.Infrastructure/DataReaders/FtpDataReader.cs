using System.Runtime.CompilerServices;
using System.Text.Json;
using FluentFTP;
using Microsoft.Extensions.Logging;
using MultiTenantETL.Application.Connectors.DataReaders;
using MultiTenantETL.Domain.Entities;
using IDataReader = MultiTenantETL.Application.Connectors.DataReaders.IDataReader;

namespace MultiTenantETL.Infrastructure.DataReaders;

/// <summary>
/// Reads files from FTP servers
/// Downloads stream and delegates to existing format-specific readers
/// </summary>
public class FtpDataReader : IDataReader
{
    private readonly CsvDataReader _csvReader;
    private readonly JsonDataReader _jsonReader;
    private readonly JsonLinesDataReader _jsonLinesReader;
    private readonly ILogger<FtpDataReader> _logger;

    public FtpDataReader(
        CsvDataReader csvReader,
        JsonDataReader jsonReader,
        JsonLinesDataReader jsonLinesReader,
        ILogger<FtpDataReader> logger)
    {
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
        
        using var client = new AsyncFtpClient(config.Host, config.Username, config.Password, config.Port);
        
        try
        {
            await client.Connect(cancellationToken);
            
            if (!await client.FileExists(config.FilePath, cancellationToken))
            {
                throw new FileNotFoundException($"File not found on FTP server: {config.FilePath}");
            }

            var format = DetermineFormat(config.FilePath, config.Format);
            
            // Download file to memory stream
            using var stream = new MemoryStream();
            await client.DownloadStream(stream, config.FilePath, token: cancellationToken);
            stream.Position = 0;

            // Delegate to format-specific reader
            var streamConnector = CreateStreamConnector(stream, format);
            var reader = GetReaderForFormat(format);

            await foreach (var batch in reader.ReadAsync(streamConnector, options, cancellationToken))
            {
                yield return batch;
            }
        }
        finally
        {
            if (client.IsConnected)
            {
                await client.Disconnect(cancellationToken);
            }
        }
    }

    public async Task<bool> TestConnectionAsync(Connector connector, CancellationToken cancellationToken)
    {
        try
        {
            var config = ParseConfig(connector.ConfigJson);
            
            using var client = new AsyncFtpClient(config.Host, config.Username, config.Password, config.Port);
            await client.Connect(cancellationToken);
            
            var exists = await client.FileExists(config.FilePath, cancellationToken);
            await client.Disconnect(cancellationToken);
            
            return exists;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "FTP connection test failed");
            return false;
        }
    }

    public async Task<SchemaDetectionResult> DetectSchemaAsync(Connector connector, CancellationToken cancellationToken)
    {
        try
        {
            var config = ParseConfig(connector.ConfigJson);
            
            using var client = new AsyncFtpClient(config.Host, config.Username, config.Password, config.Port);
            await client.Connect(cancellationToken);

            if (!await client.FileExists(config.FilePath, cancellationToken))
            {
                throw new FileNotFoundException($"File not found on FTP server: {config.FilePath}");
            }

            var format = DetermineFormat(config.FilePath, config.Format);
            
            using var stream = new MemoryStream();
            await client.DownloadStream(stream, config.FilePath, token: cancellationToken);
            stream.Position = 0;

            var streamConnector = CreateStreamConnector(stream, format);
            var reader = GetReaderForFormat(format);

            var result = await reader.DetectSchemaAsync(streamConnector, cancellationToken);
            
            await client.Disconnect(cancellationToken);
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Schema detection failed for FTP");
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
            Name = "FtpStreamConnector",
            Type = "File",
            Provider = "FTP",
            Direction = "source",
            IsSource = true,
            IsDestination = false,
            RequiresCredentials = true,
            IsActive = true,
            ConfigJson = configJson,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = Guid.Empty
        };
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

            // Validate required fields
            if (string.IsNullOrEmpty(config.Host))
                throw new InvalidOperationException("FTP host is required");
            if (string.IsNullOrEmpty(config.Username))
                throw new InvalidOperationException("FTP username is required");
            if (string.IsNullOrEmpty(config.Password))
                throw new InvalidOperationException("FTP password is required");
            if (string.IsNullOrEmpty(config.FilePath))
                throw new InvalidOperationException("FTP file path is required");

            return config;
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException("Invalid FTP configuration JSON", ex);
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
