using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using MultiTenantETL.Application.Connectors.DataReaders;
using MultiTenantETL.Domain.Entities;
using Renci.SshNet;
using IDataReader = MultiTenantETL.Application.Connectors.DataReaders.IDataReader;

namespace MultiTenantETL.Infrastructure.DataReaders;

/// <summary>
/// Reads files from SFTP servers
/// Downloads stream and delegates to existing format-specific readers
/// </summary>
public class SftpDataReader : IDataReader
{
    private readonly CsvDataReader _csvReader;
    private readonly JsonDataReader _jsonReader;
    private readonly JsonLinesDataReader _jsonLinesReader;
    private readonly ILogger<SftpDataReader> _logger;

    public SftpDataReader(
        CsvDataReader csvReader,
        JsonDataReader jsonReader,
        JsonLinesDataReader jsonLinesReader,
        ILogger<SftpDataReader> logger)
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
        
        using var client = new SftpClient(config.Host, config.Port, config.Username, config.Password);
        
        try
        {
            client.Connect();
            
            if (!client.Exists(config.FilePath))
            {
                throw new FileNotFoundException($"File not found on SFTP server: {config.FilePath}");
            }

            var format = DetermineFormat(config.FilePath, config.Format);
            
            // Download file to memory stream
            using var stream = new MemoryStream();
            client.DownloadFile(config.FilePath, stream);
            stream.Position = 0;

            // Delegate to format-specific reader
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
                AzureBlobDataReader.RemoveStreamFromRegistry(registryKey);
            }
        }
        finally
        {
            if (client.IsConnected)
            {
                client.Disconnect();
            }
        }
    }

    public async Task<bool> TestConnectionAsync(Connector connector, CancellationToken cancellationToken)
    {
        try
        {
            var config = ParseConfig(connector.ConfigJson);
            
            using var client = new SftpClient(config.Host, config.Port, config.Username, config.Password);
            client.Connect();
            
            var exists = client.Exists(config.FilePath);
            client.Disconnect();
            
            return exists;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SFTP connection test failed");
            return false;
        }
    }

    public async Task<SchemaDetectionResult> DetectSchemaAsync(Connector connector, CancellationToken cancellationToken)
    {
        try
        {
            var config = ParseConfig(connector.ConfigJson);
            
            using var client = new SftpClient(config.Host, config.Port, config.Username, config.Password);
            client.Connect();

            if (!client.Exists(config.FilePath))
            {
                throw new FileNotFoundException($"File not found on SFTP server: {config.FilePath}");
            }

            var format = DetermineFormat(config.FilePath, config.Format);
            
            using var stream = new MemoryStream();
            client.DownloadFile(config.FilePath, stream);
            stream.Position = 0;

            var streamConnector = CreateStreamConnector(stream, format, out var registryKey);
            var reader = GetReaderForFormat(format);

            try
            {
                var result = await reader.DetectSchemaAsync(streamConnector, cancellationToken);
                client.Disconnect();
                return result;
            }
            finally
            {
                AzureBlobDataReader.RemoveStreamFromRegistry(registryKey);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Schema detection failed for SFTP");
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

    private static Connector CreateStreamConnector(Stream stream, string format, out Guid registryKey)
    {
        registryKey = Guid.NewGuid();
        AzureBlobDataReader.RegisterStream(registryKey, stream);

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
            Name = "SftpStreamConnector",
            Type = "File",
            Provider = "SFTP",
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
