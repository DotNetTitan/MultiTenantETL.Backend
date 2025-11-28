using System.Text.Json;
using Microsoft.Extensions.Options;
using MultiTenantETL.Application.Connectors.DataWriters;
using MultiTenantETL.Domain.Entities;
using MultiTenantETL.Infrastructure.Configuration;

namespace MultiTenantETL.Infrastructure.DataWriters;

/// <summary>
/// Writer for NDJSON (Newline-Delimited JSON) files - optimized for streaming large datasets
/// Each line is a separate JSON object, enabling true streaming without loading entire dataset into memory
/// </summary>
public class NdjsonDataWriter : IDataWriter
{
    private readonly EtlSettings _settings;

    public NdjsonDataWriter(IOptions<EtlSettings> settings)
    {
        _settings = settings.Value;
    }

    public string ConnectorType => "NDJSON";

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

            foreach (var row in data)
            {
                try
                {
                    var json = JsonSerializer.Serialize(row);
                    await writer.WriteLineAsync(json);
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

            await using var fileStream = new FileStream(config.FilePath, FileMode.Create, FileAccess.Write, FileShare.None, _settings.FileStreamBufferSize, true);
            await using var writer = new StreamWriter(fileStream);

            await foreach (var batch in batches.WithCancellation(cancellationToken))
            {
                foreach (var row in batch)
                {
                    try
                    {
                        var json = JsonSerializer.Serialize(row);
                        await writer.WriteLineAsync(json);
                        result.RowsWritten++;
                    }
                    catch (Exception ex)
                    {
                        result.RowsFailed++;
                        result.Errors.Add($"Row {result.RowsWritten + result.RowsFailed}: {ex.Message}");
                    }
                }
            }

            await writer.FlushAsync();
        }
        catch (Exception ex)
        {
            result.Errors.Add($"Write failed: {ex.Message}");
        }

        return result;
    }

    private NdjsonConfig ParseConfig(string configJson)
    {
        return JsonSerializer.Deserialize<NdjsonConfig>(configJson) 
            ?? throw new InvalidOperationException("Invalid NDJSON configuration");
    }

    private class NdjsonConfig
    {
        public string FilePath { get; set; } = string.Empty;
    }
}
