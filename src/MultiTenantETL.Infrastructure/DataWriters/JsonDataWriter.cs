using System.Text.Json;
using Microsoft.Extensions.Options;
using MultiTenantETL.Application.Connectors.DataWriters;
using MultiTenantETL.Domain.Entities;
using MultiTenantETL.Infrastructure.Configuration;

namespace MultiTenantETL.Infrastructure.DataWriters;

public class JsonDataWriter : IDataWriter
{
    private readonly EtlSettings _settings;

    public JsonDataWriter(IOptions<EtlSettings> settings)
    {
        _settings = settings.Value;
    }

    public string ConnectorType => "JSON";

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

            var options = new JsonSerializerOptions
            {
                WriteIndented = config.Indented
            };

            var jsonContent = config.IsArray 
                ? JsonSerializer.Serialize(data, options)
                : JsonSerializer.Serialize(data[0], options);

            await File.WriteAllTextAsync(config.FilePath, jsonContent, cancellationToken);
            
            result.RowsWritten = data.Count;
        }
        catch (Exception ex)
        {
            result.Errors.Add($"Write failed: {ex.Message}");
            result.RowsFailed = data.Count;
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
            
            // Use Utf8JsonWriter for high-performance streaming JSON writing
            var options = new JsonWriterOptions { Indented = config.Indented };
            await using var jsonWriter = new Utf8JsonWriter(fileStream, options);

            if (config.IsArray)
            {
                jsonWriter.WriteStartArray();

                await foreach (var batch in batches.WithCancellation(cancellationToken))
                {
                    foreach (var row in batch)
                    {
                        jsonWriter.WriteStartObject();
                        
                        foreach (var kvp in row)
                        {
                            jsonWriter.WritePropertyName(kvp.Key);
                            WriteValue(jsonWriter, kvp.Value);
                        }
                        
                        jsonWriter.WriteEndObject();
                        result.RowsWritten++;

                        // Flush periodically to avoid memory buildup
                        if (result.RowsWritten % _settings.FileBatchSize == 0)
                        {
                            await jsonWriter.FlushAsync(cancellationToken);
                        }
                    }
                }

                jsonWriter.WriteEndArray();
            }
            else
            {
                // For single object, just write the first row
                await foreach (var batch in batches.WithCancellation(cancellationToken))
                {
                    if (batch.Count > 0)
                    {
                        jsonWriter.WriteStartObject();
                        
                        foreach (var kvp in batch[0])
                        {
                            jsonWriter.WritePropertyName(kvp.Key);
                            WriteValue(jsonWriter, kvp.Value);
                        }
                        
                        jsonWriter.WriteEndObject();
                        result.RowsWritten = 1;
                        break;
                    }
                }
            }

            await jsonWriter.FlushAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            result.Errors.Add($"Write failed: {ex.Message}");
            result.RowsFailed = result.RowsWritten;
            result.RowsWritten = 0;
        }

        return result;
    }

    private void WriteValue(Utf8JsonWriter writer, object? value)
    {
        switch (value)
        {
            case null:
                writer.WriteNullValue();
                break;
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
            case double dbl:
                writer.WriteNumberValue(dbl);
                break;
            case float f:
                writer.WriteNumberValue(f);
                break;
            case bool b:
                writer.WriteBooleanValue(b);
                break;
            case DateTime dt:
                writer.WriteStringValue(dt.ToString("O"));
                break;
            case DateTimeOffset dto:
                writer.WriteStringValue(dto.ToString("O"));
                break;
            case Guid g:
                writer.WriteStringValue(g.ToString());
                break;
            default:
                writer.WriteStringValue(value.ToString());
                break;
        }
    }

    private JsonConfig ParseConfig(string configJson)
    {
        return JsonSerializer.Deserialize<JsonConfig>(configJson) 
            ?? throw new InvalidOperationException("Invalid JSON configuration");
    }

    private class JsonConfig
    {
        public string FilePath { get; set; } = string.Empty;
        public bool IsArray { get; set; } = true;
        public bool Indented { get; set; } = true;
    }
}
