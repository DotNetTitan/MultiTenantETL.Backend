using Microsoft.Extensions.Logging;
using MultiTenantETL.Application.Connectors.DataWriters;
using MultiTenantETL.Domain.Constants;
using MultiTenantETL.Domain.Entities;
using MultiTenantETL.Infrastructure.DataWriters;

namespace MultiTenantETL.Infrastructure.DataAccess.Writers.File;

/// <summary>
/// Factory for creating file-specific data writers based on provider and format
/// </summary>
public class FileDataWriterFactory : IFileDataWriterFactory
{
    private readonly CsvDataWriter _csvWriter;
    private readonly JsonDataWriter _jsonWriter;
    private readonly JsonLinesDataWriter _jsonLinesWriter;
    private readonly S3DataWriter _s3Writer;
    private readonly AzureBlobDataWriter _azureBlobWriter;
    private readonly ILogger<FileDataWriterFactory> _logger;

    public FileDataWriterFactory(
        CsvDataWriter csvWriter,
        JsonDataWriter jsonWriter,
        JsonLinesDataWriter jsonLinesWriter,
        S3DataWriter s3Writer,
        AzureBlobDataWriter azureBlobWriter,
        ILogger<FileDataWriterFactory> logger)
    {
        _csvWriter = csvWriter;
        _jsonWriter = jsonWriter;
        _jsonLinesWriter = jsonLinesWriter;
        _s3Writer = s3Writer;
        _azureBlobWriter = azureBlobWriter;
        _logger = logger;
    }

    public IDataWriter CreateWriter(Connector connector)
    {
        _logger.LogDebug("Creating file writer for provider {Provider}", connector.Provider);

        // Cloud storage providers handle their own format
        return connector.Provider switch
        {
            ConnectorProviders.S3 => _s3Writer,
            ConnectorProviders.AzureBlob => _azureBlobWriter,
            ConnectorProviders.FTP => throw new NotImplementedException("FTP writer not yet implemented"),
            ConnectorProviders.SFTP => throw new NotImplementedException("SFTP writer not yet implemented"),
            ConnectorProviders.Local => CreateLocalFileWriter(connector),
            _ => throw new NotSupportedException($"File provider '{connector.Provider}' is not supported")
        };
    }

    private IDataWriter CreateLocalFileWriter(Connector connector)
    {
        // Determine format from config or file extension for local files
        var format = DetermineFormat(connector);

        return format.ToLower() switch
        {
            "csv" => _csvWriter,
            "json" => _jsonWriter,
            "jsonl" or "jsonlines" => _jsonLinesWriter,
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
