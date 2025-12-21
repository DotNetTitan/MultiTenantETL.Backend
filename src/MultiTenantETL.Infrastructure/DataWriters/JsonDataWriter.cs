using System.Text.Json;
using Microsoft.Extensions.Logging;
using MultiTenantETL.Application.Connectors.DataReaders;
using MultiTenantETL.Application.Connectors.DataWriters;
using MultiTenantETL.Domain.Entities;

namespace MultiTenantETL.Infrastructure.DataWriters;

public class JsonDataWriter : IDataWriter
{
    private readonly ILogger<JsonDataWriter> _logger;

    public JsonDataWriter(ILogger<JsonDataWriter> logger)
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

            if (config.Stream != null)
            {
                // Stream mode - write directly (caller manages array structure)
                await WriteJsonArrayAsync(config.Stream, batch.Rows, config.Indented, cancellationToken);
            }
            else
            {
                // File mode - JSON arrays don't support true streaming append
                // For production use, recommend JSONL format instead for large datasets
                if (!options.TruncateBeforeLoad && File.Exists(config.FilePath))
                {
                    throw new NotSupportedException("JSON array format does not support efficient append operations. Use JSONL format for append scenarios or truncate the file.");
                }
                else
                {
                    // Truncate or new file
                    await using var writeStream = File.Create(config.FilePath);
                    await WriteJsonArrayAsync(writeStream, batch.Rows, config.Indented, cancellationToken);
                }
            }

            result.RowsWritten = batch.RowCount;
            result.RowsFailed = 0;
        }
        catch (NotSupportedException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write batch to JSON");
            result.RowsFailed = batch.RowCount;
            result.Errors.Add(ex.Message);
        }

        return result;
    }

    private async Task WriteJsonArrayAsync(
        Stream stream,
        IEnumerable<Dictionary<string, object?>> rows,
        bool indented,
        CancellationToken cancellationToken)
    {
        var options = new JsonWriterOptions
        {
            Indented = indented
        };

        await using var writer = new Utf8JsonWriter(stream, options);

        writer.WriteStartArray();

        foreach (var row in rows)
        {
            writer.WriteStartObject();

            foreach (var kvp in row)
            {
                writer.WritePropertyName(kvp.Key);

                if (kvp.Value == null)
                {
                    writer.WriteNullValue();
                }
                else
                {
                    switch (kvp.Value)
                    {
                        case string s:
                            writer.WriteStringValue(s);
                            break;
                        case int i:
                            writer.WriteNumberValue(i);
                            break;
                        case long l:
                            writer.WriteNumberValue(l);
                            break;
                        case decimal d:
                            writer.WriteNumberValue(d);
                            break;
                        case double db:
                            writer.WriteNumberValue(db);
                            break;
                        case float f:
                            writer.WriteNumberValue(f);
                            break;
                        case bool b:
                            writer.WriteBooleanValue(b);
                            break;
                        case DateTime dt:
                            writer.WriteStringValue(dt);
                            break;
                        case DateTimeOffset dto:
                            writer.WriteStringValue(dto);
                            break;
                        default:
                            writer.WriteStringValue(kvp.Value.ToString());
                            break;
                    }
                }
            }

            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        await writer.FlushAsync(cancellationToken);
    }

    private JsonConfig ParseConfig(string configJson)
    {
        return JsonSerializer.Deserialize<JsonConfig>(configJson)
            ?? throw new InvalidOperationException("Invalid JSON configuration");
    }

    public ValueTask DisposeAsync()
    {
        // No resources to dispose for local file writer
        return ValueTask.CompletedTask;
    }

    private class JsonConfig
    {
        public string FilePath { get; set; } = string.Empty;
        public bool Indented { get; set; } = true;
        public Stream? Stream { get; set; }
    }
}
