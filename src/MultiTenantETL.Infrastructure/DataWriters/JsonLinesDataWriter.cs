using System.Text.Json;
using Microsoft.Extensions.Logging;
using MultiTenantETL.Application.Connectors.DataReaders;
using MultiTenantETL.Application.Connectors.DataWriters;
using MultiTenantETL.Domain.Entities;

namespace MultiTenantETL.Infrastructure.DataWriters;

/// <summary>
/// Writes JSONL (JSON Lines / newline-delimited JSON) files - one JSON object per line
/// This format is ideal for streaming large datasets and supports true append without reading entire file
/// </summary>
public class JsonLinesDataWriter : IDataWriter
{
    private readonly ILogger<JsonLinesDataWriter> _logger;

    public JsonLinesDataWriter(ILogger<JsonLinesDataWriter> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<DataWriteResult> WriteBatchAsync(
        Connector connector,
        ReadBatch batch,
        WriteOptions options,
        CancellationToken cancellationToken)
    {
        var result = new DataWriteResult { BatchId = batch.BatchId };

        try
        {
            var config = ParseConfig(connector.ConfigJson);

            StreamWriter writer;
            bool ownsStream = false;

            if (config.Stream != null)
            {
                writer = new StreamWriter(config.Stream, leaveOpen: true);
            }
            else
            {
                var fileMode = options.TruncateBeforeLoad || !File.Exists(config.FilePath)
                    ? FileMode.Create
                    : FileMode.Append;
                writer = new StreamWriter(config.FilePath, fileMode == FileMode.Append);
                ownsStream = true;
            }

            try
            {
                foreach (var row in batch.Rows)
                {
                    var json = JsonSerializer.Serialize(row);
                    await writer.WriteLineAsync(json);
                }

                await writer.FlushAsync();

                result.RowsWritten = batch.RowCount;
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
            _logger.LogError(ex, "Failed to write batch to JSONL");
            result.RowsFailed = batch.RowCount;
            result.Errors.Add(ex.Message);
        }

        return result;
    }

    private JsonLinesConfig ParseConfig(string configJson)
    {
        return JsonSerializer.Deserialize<JsonLinesConfig>(configJson)
            ?? throw new InvalidOperationException("Invalid JSONL configuration");
    }

    public ValueTask DisposeAsync()
    {
        // No resources to dispose for local file writer
        return ValueTask.CompletedTask;
    }

    private class JsonLinesConfig
    {
        public string FilePath { get; set; } = string.Empty;
        public Stream? Stream { get; set; }
    }
}
