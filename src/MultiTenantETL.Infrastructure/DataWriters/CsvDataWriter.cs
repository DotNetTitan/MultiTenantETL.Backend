using System.Globalization;
using System.Text.Json;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.Extensions.Options;
using MultiTenantETL.Application.Connectors.DataWriters;
using MultiTenantETL.Domain.Entities;
using MultiTenantETL.Infrastructure.Configuration;

namespace MultiTenantETL.Infrastructure.DataWriters;

public class CsvDataWriter : IDataWriter
{
    private readonly EtlSettings _settings;

    public CsvDataWriter(IOptions<EtlSettings> settings)
    {
        _settings = settings.Value;
    }

    public string ConnectorType => "CSV";

    public async Task<DataWriteResult> WriteAsync(
        Connector connector,
        List<Dictionary<string, object?>> data,
        CancellationToken cancellationToken = default)
    {
        var config = ParseConfig(connector.ConfigJson);
        var result = new DataWriteResult();

        if (data.Count == 0)
        {
            result.Warnings.Add("No data to write");
            return result;
        }

        try
        {
            // Ensure directory exists
            var directory = Path.GetDirectoryName(config.FilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await using var writer = new StreamWriter(config.FilePath);
            await using var csv = new CsvWriter(writer, new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                Delimiter = config.Delimiter
            });

            // Write header
            var headers = data[0].Keys.ToList();
            foreach (var header in headers)
            {
                csv.WriteField(header);
            }
            await csv.NextRecordAsync();

            // Write data
            foreach (var row in data)
            {
                try
                {
                    foreach (var header in headers)
                    {
                        csv.WriteField(row.ContainsKey(header) ? row[header] : null);
                    }
                    await csv.NextRecordAsync();
                    result.RowsWritten++;
                }
                catch (Exception ex)
                {
                    result.RowsFailed++;
                    result.Errors.Add($"Row {result.RowsWritten + result.RowsFailed}: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            result.Errors.Add($"Write failed: {ex.Message}");
        }

        return result;
    }

    public async Task<DataWriteResult> WriteBatchesAsync(
        Connector connector,
        IAsyncEnumerable<List<Dictionary<string, object?>>> batches,
        CancellationToken cancellationToken = default)
    {
        var config = ParseConfig(connector.ConfigJson);
        var result = new DataWriteResult();

        try
        {
            var directory = Path.GetDirectoryName(config.FilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await using var writer = new StreamWriter(config.FilePath);
            await using var csv = new CsvWriter(writer, new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                Delimiter = config.Delimiter
            });

            List<string>? headers = null;
            bool headerWritten = false;

            await foreach (var batch in batches.WithCancellation(cancellationToken))
            {
                if (batch.Count == 0) continue;

                // Write header from first batch
                if (!headerWritten)
                {
                    headers = batch[0].Keys.ToList();
                    foreach (var header in headers)
                    {
                        csv.WriteField(header);
                    }
                    await csv.NextRecordAsync();
                    headerWritten = true;
                }

                foreach (var row in batch)
                {
                    try
                    {
                        foreach (var header in headers!)
                        {
                            csv.WriteField(row.ContainsKey(header) ? row[header] : null);
                        }
                        await csv.NextRecordAsync();
                        result.RowsWritten++;
                    }
                    catch (Exception ex)
                    {
                        result.RowsFailed++;
                        result.Errors.Add($"Row {result.RowsWritten + result.RowsFailed}: {ex.Message}");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            result.Errors.Add($"Write failed: {ex.Message}");
        }

        return result;
    }

    private CsvConfig ParseConfig(string configJson)
    {
        return JsonSerializer.Deserialize<CsvConfig>(configJson) 
            ?? throw new InvalidOperationException("Invalid CSV configuration");
    }

    private class CsvConfig
    {
        public string FilePath { get; set; } = string.Empty;
        public string Delimiter { get; set; } = ",";
    }
}
