using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Xml;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.Extensions.Logging;
using MultiTenantETL.Application.Connectors.DataReaders;
using MultiTenantETL.Application.Connectors.DataWriters;
using MultiTenantETL.Application.Interfaces;
using MultiTenantETL.Domain.Constants;
using MultiTenantETL.Domain.Entities;
using MultiTenantETL.Infrastructure.Services;
using OfficeOpenXml;

namespace MultiTenantETL.Infrastructure.DataWriters;

/// <summary>
/// Writes pipeline data as an email attachment.
/// Buffers all rows in WriteBatchAsync and sends exactly one email in DisposeAsync.
/// The email body contains only a summary; all data goes into the file attachment.
/// </summary>
public class EmailDataWriter : IDataWriter
{
    private readonly IEmailService _emailService;
    private readonly ILogger<EmailDataWriter> _logger;
    private readonly List<Dictionary<string, object?>> _bufferedRows = new();
    private Connector? _connector;

    public EmailDataWriter(IEmailService emailService, ILogger<EmailDataWriter> logger)
    {
        _emailService = emailService ?? throw new ArgumentNullException(nameof(emailService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Buffers batch rows in memory. Does NOT send the email yet.
    /// The email is sent in DisposeAsync after all batches have been buffered.
    /// </summary>
    public Task<DataWriteResult> WriteBatchAsync(
        Connector connector,
        ReadBatch batch,
        WriteOptions options,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var result = new DataWriteResult { BatchId = batch.BatchId };

        _connector ??= connector;

        foreach (var row in batch.Rows)
        {
            _bufferedRows.Add(row);
        }

        result.RowsWritten = batch.RowCount;
        result.RowsFailed = 0;

        _logger.LogDebug("Buffered {RowCount} rows for email export (total buffered: {TotalRows})",
            batch.RowCount, _bufferedRows.Count);

        return Task.FromResult(result);
    }

    /// <summary>
    /// Sends exactly one email with all buffered data as a file attachment.
    /// If no rows were buffered and sendEmptyReport is false, skips sending.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        try
        {
            if (_connector == null)
            {
                _logger.LogDebug("No connector set; skipping email send");
                return;
            }

            var config = ParseConfig(_connector.ConfigJson);

            var sendEmptyReport = config.WriteConfig?.SendEmptyReport ?? config.SendEmptyReport;

            if (_bufferedRows.Count == 0 && !sendEmptyReport)
            {
                _logger.LogInformation("No data rows to export and sendEmptyReport is false; skipping email");
                return;
            }

            // Format the data as a file attachment
            var (attachmentBytes, mediaType) = FormatAttachment(config.AttachmentFormat);

            var fileName = EmailTemplates.BuildExportFileName(config.AttachmentFileName, config.AttachmentFormat);

            // Generate summary-only HTML body
            var htmlBody = EmailTemplates.GetDataExportEmail(
                config.BodyMessage,
                _bufferedRows.Count,
                config.AttachmentFormat ?? MetadataConstants.FileFormats.DefaultFormat,
                fileName);

            var success = await _emailService.SendDataExportEmailAsync(
                config.Recipients,
                config.CcRecipients,
                config.Subject ?? "Data Export",
                htmlBody,
                fileName,
                mediaType,
                attachmentBytes);

            if (success)
            {
                _logger.LogInformation(
                    "Email sent successfully to {RecipientCount} recipients with {RowCount} rows as {Format} attachment",
                    config.Recipients.Count, _bufferedRows.Count, config.AttachmentFormat);
            }
            else
            {
                _logger.LogError(
                    "Failed to send data export email to {RecipientCount} recipients",
                    config.Recipients.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during email data export disposal");
        }
        finally
        {
            _bufferedRows.Clear();
            _connector = null;
        }
    }

    private (byte[] Bytes, string MediaType) FormatAttachment(string? format)
    {
        var normalized = format?.Trim() ?? "";
        if (normalized.Equals("JSON", StringComparison.OrdinalIgnoreCase))
            return (FormatAsJson(), "application/json");
        if (normalized.Equals("Excel", StringComparison.OrdinalIgnoreCase))
            return (FormatAsExcel(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        if (normalized.Equals("XML", StringComparison.OrdinalIgnoreCase))
            return (FormatAsXml(), "application/xml");
        return (FormatAsCsv(), "text/csv");
    }

    private byte[] FormatAsCsv()
    {
        using var memoryStream = new MemoryStream();
        using var writer = new StreamWriter(memoryStream, Encoding.UTF8);
        var csvConfig = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true
        };
        using var csv = new CsvWriter(writer, csvConfig);

        if (_bufferedRows.Count == 0)
        {
            writer.Flush();
            return memoryStream.ToArray();
        }

        var headers = _bufferedRows[0].Keys.ToList();

        // Write headers
        foreach (var header in headers)
        {
            csv.WriteField(header);
        }
        csv.NextRecord();

        // Write data rows
        foreach (var row in _bufferedRows)
        {
            foreach (var header in headers)
            {
                csv.WriteField(row.TryGetValue(header, out var value) ? value : null);
            }
            csv.NextRecord();
        }

        writer.Flush();
        return memoryStream.ToArray();
    }

    private byte[] FormatAsJson()
    {
        return JsonSerializer.SerializeToUtf8Bytes(_bufferedRows, new JsonSerializerOptions
        {
            WriteIndented = true
        });
    }

    private byte[] FormatAsExcel()
    {
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        using var package = new ExcelPackage();
        var worksheet = package.Workbook.Worksheets.Add("Data Export");

        if (_bufferedRows.Count == 0)
        {
            return package.GetAsByteArray();
        }

        var headers = _bufferedRows[0].Keys.ToList();

        // Write headers
        for (var col = 0; col < headers.Count; col++)
        {
            worksheet.Cells[1, col + 1].Value = headers[col];
        }

        // Write data rows
        for (var rowIndex = 0; rowIndex < _bufferedRows.Count; rowIndex++)
        {
            var row = _bufferedRows[rowIndex];
            for (var col = 0; col < headers.Count; col++)
            {
                worksheet.Cells[rowIndex + 2, col + 1].Value =
                    row.TryGetValue(headers[col], out var value) ? value : null;
            }
        }

        worksheet.Cells.AutoFitColumns();
        return package.GetAsByteArray();
    }

    private byte[] FormatAsXml()
    {
        using var memoryStream = new MemoryStream();
        using var writer = XmlWriter.Create(memoryStream, new XmlWriterSettings
        {
            Indent = true,
            Encoding = Encoding.UTF8
        });

        writer.WriteStartDocument();
        writer.WriteStartElement("DataExport");

        if (_bufferedRows.Count > 0)
        {
            var headers = _bufferedRows[0].Keys.ToList();

            foreach (var row in _bufferedRows)
            {
                writer.WriteStartElement("Row");
                foreach (var header in headers)
                {
                    var value = row.TryGetValue(header, out var v) ? v?.ToString() ?? "" : "";
                    writer.WriteElementString(SanitizeXmlElementName(header), value);
                }
                writer.WriteEndElement();
            }
        }

        writer.WriteEndElement();
        writer.WriteEndDocument();
        writer.Flush();
        return memoryStream.ToArray();
    }

    /// <summary>
    /// Ensures a string is a valid XML element name by replacing invalid characters.
    /// </summary>
    private static string SanitizeXmlElementName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "Field";

        var sb = new StringBuilder(name.Length);
        for (var i = 0; i < name.Length; i++)
        {
            var c = name[i];
            if (i == 0 && !XmlConvert.IsStartNCNameChar(c))
                sb.Append('_');
            else if (!XmlConvert.IsNCNameChar(c))
                sb.Append('_');
            else
                sb.Append(c);
        }
        return sb.Length == 0 ? "Field" : sb.ToString();
    }

    private EmailConfig ParseConfig(string configJson)
    {
        try
        {
            var config = JsonSerializer.Deserialize<EmailConfig>(configJson, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? throw new InvalidOperationException("Invalid Email connector configuration");

            if (config.Recipients == null || config.Recipients.Count == 0)
                throw new InvalidOperationException("At least one recipient email address is required");

            if (string.IsNullOrWhiteSpace(config.Subject))
                throw new InvalidOperationException("Email subject is required");

            return config;
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException("Invalid Email connector configuration JSON", ex);
        }
    }

    private class EmailConfig
    {
        public List<string> Recipients { get; set; } = new();
        public List<string>? CcRecipients { get; set; }
        public string? Subject { get; set; }
        public string? BodyMessage { get; set; }
        public string? AttachmentFormat { get; set; }
        public string? AttachmentFileName { get; set; }
        public bool SendEmptyReport { get; set; }
        public EmailWriteConfigInternal? WriteConfig { get; set; }
    }

    private class EmailWriteConfigInternal
    {
        public bool SendEmptyReport { get; set; }
    }
}
