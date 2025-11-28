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
    private readonly ILogger<FileDataReaderFactory> _logger;

    public FileDataReaderFactory(
        CsvDataReader csvReader,
        JsonDataReader jsonReader,
        JsonLinesDataReader jsonLinesReader,
        S3DataReader s3Reader,
        AzureBlobDataReader azureBlobReader,
        ILogger<FileDataReaderFactory> logger)
    {
        _csvReader = csvReader;
        _jsonReader = jsonReader;
        _jsonLinesReader = jsonLinesReader;
        _s3Reader = s3Reader;
        _azureBlobReader = azureBlobReader;
        _logger = logger;
    }

    public IDataReader CreateReader(Connector connector)
    {
        _logger.LogDebug("Creating file reader for provider {Provider}", connector.Provider);

        // Cloud storage providers handle their own format detection
        return connector.Provider switch
        {
            ConnectorProviders.S3 => _s3Reader,
            ConnectorProviders.AzureBlob => _azureBlobReader,
            ConnectorProviders.FTP => throw new NotImplementedException("FTP reader not yet implemented"),
            ConnectorProviders.SFTP => throw new NotImplementedException("SFTP reader not yet implemented"),
            ConnectorProviders.Local => CreateLocalFileReader(connector),
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
        // Try to parse format from config
        try
        {
            var config = System.Text.Json.JsonSerializer.Deserialize<FileFormatConfig>(connector.ConfigJson);
            if (!string.IsNullOrEmpty(config?.Format))
            {
                return config.Format;
            }

            // Fall back to file extension
            if (!string.IsNullOrEmpty(config?.FilePath))
            {
                var extension = Path.GetExtension(config.FilePath).TrimStart('.');
                if (!string.IsNullOrEmpty(extension))
                {
                    return extension;
                }
            }
        }
        catch
        {
            // If parsing fails, default to CSV
        }

        return "csv";
    }

    private class FileFormatConfig
    {
        public string? Format { get; set; }
        public string? FilePath { get; set; }
    }
}
