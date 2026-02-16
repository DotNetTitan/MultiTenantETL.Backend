using Microsoft.Extensions.Logging;
using MultiTenantETL.Application.Connectors.DataReaders;
using MultiTenantETL.Domain.Constants;
using MultiTenantETL.Domain.Entities;
using MultiTenantETL.Infrastructure.DataReaders;

namespace MultiTenantETL.Infrastructure.DataAccess.Readers.File;

/// <summary>
/// Factory for creating file-specific data readers based on provider and format
/// </summary>
public class FileDataReaderFactory : IFileDataReaderFactory
{
    private readonly CsvDataReader _csvReader;
    private readonly JsonDataReader _jsonReader;
    private readonly JsonLinesDataReader _jsonLinesReader;
    private readonly S3DataReader _s3Reader;
    private readonly AzureBlobDataReader _azureBlobReader;
    private readonly SftpDataReader _sftpReader;
    private readonly FtpDataReader _ftpReader;
    private readonly GcsDataReader _gcsReader;
    private readonly ILogger<FileDataReaderFactory> _logger;

    public FileDataReaderFactory(
        CsvDataReader csvReader,
        JsonDataReader jsonReader,
        JsonLinesDataReader jsonLinesReader,
        S3DataReader s3Reader,
        AzureBlobDataReader azureBlobReader,
        SftpDataReader sftpReader,
        FtpDataReader ftpReader,
        GcsDataReader gcsReader,
        ILogger<FileDataReaderFactory> logger)
    {
        _csvReader = csvReader;
        _jsonReader = jsonReader;
        _jsonLinesReader = jsonLinesReader;
        _s3Reader = s3Reader;
        _azureBlobReader = azureBlobReader;
        _sftpReader = sftpReader;
        _ftpReader = ftpReader;
        _gcsReader = gcsReader;
        _logger = logger;
    }

    public IDataReader CreateReader(Connector connector)
    {
        _logger.LogDebug("Creating file reader for provider {Provider}", connector.Provider);

        // Cloud storage and remote file providers handle their own format detection
        return connector.Provider switch
        {
            ConnectorProviders.S3 => _s3Reader,
            ConnectorProviders.AzureBlob => _azureBlobReader,
            ConnectorProviders.SFTP => _sftpReader,
            ConnectorProviders.FTP => _ftpReader,
            ConnectorProviders.GCS => _gcsReader,
            _ => throw new NotSupportedException($"File provider '{connector.Provider}' is not supported")
        };
    }

    private IDataReader CreateLocalFileReader(Connector connector)
    {
        // Determine format from config or file extension for local files
        var format = DetermineFormat(connector);

        return format.ToLower() switch
        {
            "csv" => _csvReader,
            "json" => _jsonReader,
            "jsonl" or "jsonlines" => _jsonLinesReader,
            _ => throw new NotSupportedException($"File format '{format}' is not supported")
        };
    }

    private string DetermineFormat(Connector connector)
    {
        FileFormatConfig? config;
        
        try
        {
            config = System.Text.Json.JsonSerializer.Deserialize<FileFormatConfig>(connector.ConfigJson);
        }
        catch (System.Text.Json.JsonException ex)
        {
            _logger.LogError(ex, "Invalid connector configuration JSON");
            throw new InvalidOperationException("Failed to parse connector configuration", ex);
        }

        if (config == null)
        {
            throw new InvalidOperationException("Connector configuration is null");
        }

        // Use explicit format if provided
        if (!string.IsNullOrEmpty(config.Format))
        {
            return config.Format;
        }

        // Fall back to file extension
        if (!string.IsNullOrEmpty(config.FilePath))
        {
            var extension = Path.GetExtension(config.FilePath).TrimStart('.');
            if (!string.IsNullOrEmpty(extension))
            {
                return extension;
            }
        }

        throw new InvalidOperationException("Cannot determine file format: no format specified and no file extension found");
    }

    private class FileFormatConfig
    {
        public string? Format { get; set; }
        public string? FilePath { get; set; }
    }
}
