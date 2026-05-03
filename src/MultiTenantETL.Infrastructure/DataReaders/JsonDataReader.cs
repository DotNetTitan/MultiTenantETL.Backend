using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MultiTenantETL.Application.Connectors.DataReaders;
using MultiTenantETL.Domain.Entities;
using MultiTenantETL.Infrastructure.Configuration;
using IDataReader = MultiTenantETL.Application.Connectors.DataReaders.IDataReader;

namespace MultiTenantETL.Infrastructure.DataReaders;

public class JsonDataReader : IDataReader
{
    private readonly ILogger<JsonDataReader> _logger;
    private readonly EtlSettings _settings;

    public JsonDataReader(ILogger<JsonDataReader> logger, IOptions<EtlSettings> settings)
    {
        _logger = logger;
        _settings = settings.Value;
    }

    public async IAsyncEnumerable<ReadBatch> ReadAsync(
        Connector connector,
        ReadOptions options,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var config = ParseConfig(connector.ConfigJson);

        Stream stream;
        bool ownsStream = false;

        // Check if a pre-opened stream was registered (e.g. from AzureBlobDataReader)
        if (config.StreamRegistryKey.HasValue)
        {
            stream = AzureBlobDataReader.GetStreamFromRegistry(config.StreamRegistryKey.Value)
                ?? throw new InvalidOperationException($"Stream registry key {config.StreamRegistryKey.Value} not found");
        }
        else
        {
            stream = File.OpenRead(config.FilePath);
            ownsStream = true;
        }

        try
        {
            await using var fileStream = ownsStream ? stream : null;

            if (!config.IsArray)
            {
                // Single object - read entire file (should be small)
                var singleRow = await JsonSerializer.DeserializeAsync<Dictionary<string, object?>>(stream, cancellationToken: cancellationToken);
                if (singleRow != null)
                {
                    // Convert JsonElement values to .NET types
                    var convertedRow = ConvertJsonElements(singleRow);
                    yield return new ReadBatch
                    {
                        BatchId = Guid.NewGuid(),
                        Rows = new List<Dictionary<string, object?>> { convertedRow },
                        RowCount = 1
                    };
                }
                yield break;
            }

            // Use DeserializeAsyncEnumerable for true streaming - this is the recommended approach in .NET 8+
            // It reads the JSON array incrementally without loading the entire file into memory
            var jsonOptions = new JsonSerializerOptions
            {
                DefaultBufferSize = _settings.StreamBufferSize
            };

            var asyncEnumerable = JsonSerializer.DeserializeAsyncEnumerable<Dictionary<string, object?>>(
                stream,
                jsonOptions,
                cancellationToken);

            var batch = new ReadBatch { BatchId = Guid.NewGuid() };
            var rowsRead = 0;

            await foreach (var row in asyncEnumerable.WithCancellation(cancellationToken))
            {
                if (row == null)
                    continue;

                // Convert JsonElement values to .NET types
                var convertedRow = ConvertJsonElements(row);
                batch.Rows.Add(convertedRow);
                batch.RowCount++;
                rowsRead++;

                if (batch.RowCount >= options.BatchSize)
                {
                    yield return batch;
                    batch = new ReadBatch { BatchId = Guid.NewGuid() };
                }

                if (options.MaxRows.HasValue && rowsRead >= options.MaxRows.Value)
                {
                    break;
                }
            }

            if (batch.RowCount > 0)
            {
                yield return batch;
            }
        }
        finally
        {
            if (ownsStream)
            {
                stream.Dispose();
            }
        }
    }

    public Task<bool> TestConnectionAsync(Connector connector, CancellationToken cancellationToken)
    {
        try
        {
            var config = ParseConfig(connector.ConfigJson);
            return Task.FromResult(File.Exists(config.FilePath));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "JSON file test failed");
            return Task.FromResult(false);
        }
    }

    public async Task<SchemaDetectionResult> DetectSchemaAsync(Connector connector, CancellationToken cancellationToken)
    {
        try
        {
            var config = ParseConfig(connector.ConfigJson);

            await using var fileStream = File.OpenRead(config.FilePath);
            using var jsonDoc = await JsonDocument.ParseAsync(fileStream, cancellationToken: cancellationToken);

            Dictionary<string, object?>? firstRow = null;

            if (config.IsArray)
            {
                if (jsonDoc.RootElement.ValueKind == JsonValueKind.Array && jsonDoc.RootElement.GetArrayLength() > 0)
                {
                    var firstElement = jsonDoc.RootElement[0];
                    firstRow = new Dictionary<string, object?>();
                    foreach (var property in firstElement.EnumerateObject())
                    {
                        firstRow[property.Name] = null; // Just need keys for schema
                    }
                }
            }
            else
            {
                if (jsonDoc.RootElement.ValueKind == JsonValueKind.Object)
                {
                    firstRow = new Dictionary<string, object?>();
                    foreach (var property in jsonDoc.RootElement.EnumerateObject())
                    {
                        firstRow[property.Name] = null;
                    }
                }
            }

            var fields = firstRow?.Keys.Select(k => new FieldDefinition
            {
                Name = k,
                DataType = "string",
                IsNullable = true,
                IsPrimaryKey = false
            }).ToList() ?? new List<FieldDefinition>();

            return new SchemaDetectionResult
            {
                Fields = fields,
                Version = 1,
                DetectedAt = DateTimeOffset.UtcNow,
                Success = true
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Schema detection failed for JSON");
            return new SchemaDetectionResult
            {
                Success = false,
                ErrorMessage = ex.Message,
                DetectedAt = DateTimeOffset.UtcNow
            };
        }
    }

    private static Dictionary<string, object?> ConvertJsonElements(Dictionary<string, object?> dict)
    {
        var result = new Dictionary<string, object?>();
        foreach (var kvp in dict)
        {
            result[kvp.Key] = ConvertJsonElement(kvp.Value);
        }
        return result;
    }

    private static object? ConvertJsonElement(object? value)
    {
        if (value is JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.String => element.GetString(),
                JsonValueKind.Number => element.TryGetInt32(out var intValue) ? intValue : element.GetDouble(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Null => null,
                JsonValueKind.Object => ConvertJsonElements(element.Deserialize<Dictionary<string, object?>>() ?? new Dictionary<string, object?>()),
                JsonValueKind.Array => element.EnumerateArray().Select(x => ConvertJsonElement(x)).ToList(),
                _ => element.ToString()
            };
        }
        return value;
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
        /// <summary>Key into <see cref="AzureBlobDataReader._streamRegistry"/> for cloud-streamed blobs.</summary>
        public Guid? StreamRegistryKey { get; set; }
    }
}
