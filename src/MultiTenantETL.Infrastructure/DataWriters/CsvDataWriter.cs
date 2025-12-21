using System.Globalization;
using System.Text.Json;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.Extensions.Logging;
using MultiTenantETL.Application.Connectors.DataReaders;
using MultiTenantETL.Application.Connectors.DataWriters;
using MultiTenantETL.Domain.Entities;

namespace MultiTenantETL.Infrastructure.DataWriters;

public class CsvDataWriter : IDataWriter
{
    private readonly ILogger<CsvDataWriter> _logger;

    public CsvDataWriter(ILogger<CsvDataWriter> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<DataWriteResult> WriteBatchAsync(
        Connector connector,
        ReadBatch batch,
        WriteOptions options,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        
        var result = new DataWriteResult { BatchId = batch.BatchId };

        var config = ParseConfig(connector.ConfigJson);
        
        try
        {

            StreamWriter writer;
            bool ownsStream = false;
            bool writeHeader = false;

            if (config.Stream != null)
            {
                writer = new StreamWriter(config.Stream, leaveOpen: true);
                writeHeader = config.HasHeader; // For streams, caller controls header
            }
            else
            {
                var fileMode = options.TruncateBeforeLoad || !File.Exists(config.FilePath)
                    ? FileMode.Create
                    : FileMode.Append;
                writer = new StreamWriter(config.FilePath, fileMode == FileMode.Append);
                writeHeader = fileMode == FileMode.Create && config.HasHeader;
                ownsStream = true;
            }

            try
            {
                var csvConfig = new CsvConfiguration(CultureInfo.InvariantCulture)
                {
                    HasHeaderRecord = writeHeader,
                    Delimiter = config.Delimiter ?? ","
                };

                await using var csv = new CsvWriter(writer, csvConfig);

                if (batch.Rows.Count == 0)
                    return result;

                var headers = batch.Rows[0].Keys.ToList();

                if (csvConfig.HasHeaderRecord)
                {
                    foreach (var header in headers)
                    {
                        csv.WriteField(header);
                    }
                    await csv.NextRecordAsync();
                }

                foreach (var row in batch.Rows)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    
                    foreach (var header in headers)
                    {
                        csv.WriteField(row[header]);
                    }
                    await csv.NextRecordAsync();
                }

                await csv.FlushAsync();

                result.RowsWritten = batch.Rows.Count;
                result.RowsFailed = 0;
            }
            finally
            {
                if (ownsStream)
                {
                    await writer.DisposeAsync();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write batch to CSV");
            result.RowsFailed = batch.RowCount;
            result.Errors.Add(ex.Message);
        }

        return result;
    }

    private CsvConfig ParseConfig(string configJson)
    {
        try
        {
            return JsonSerializer.Deserialize<CsvConfig>(configJson)
                ?? throw new InvalidOperationException("Invalid CSV configuration");
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException("Failed to parse CSV connector configuration", ex);
        }
    }

    public ValueTask DisposeAsync()
    {
        // No resources to dispose for local file writer
        return ValueTask.CompletedTask;
    }

    private class CsvConfig
    {
        public string FilePath { get; set; } = string.Empty;
        public bool HasHeader { get; set; } = true;
        public string? Delimiter { get; set; }
        public Stream? Stream { get; set; }
    }
}
