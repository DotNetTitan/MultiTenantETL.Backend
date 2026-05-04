using Microsoft.Extensions.Logging;
using MultiTenantETL.Application.Connectors.DataReaders;
using MultiTenantETL.Domain.Entities;
using System.Runtime.CompilerServices;
using System.Text.Json;
using IDataReader = MultiTenantETL.Application.Connectors.DataReaders.IDataReader;

namespace MultiTenantETL.Infrastructure.DataReaders;

/// <summary>
/// Reads JSONL (JSON Lines / newline-delimited JSON) files - one JSON object per line
/// This format is ideal for streaming large datasets without loading entire file into memory
/// </summary>
public class JsonLinesDataReader : IDataReader
{
    private readonly ILogger<JsonLinesDataReader> _logger;

    public JsonLinesDataReader(ILogger<JsonLinesDataReader> logger)
    {
        _logger = logger;
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
            using var reader = new StreamReader(stream, leaveOpen: !ownsStream);

            var batch = new ReadBatch { BatchId = Guid.NewGuid() };
            var rowsRead = 0;

            while (!reader.EndOfStream)
            {
                var line = await reader.ReadLineAsync(cancellationToken);

                if (string.IsNullOrWhiteSpace(line))
                    continue;

                var row = JsonSerializer.Deserialize<Dictionary<string, object?>>(line);
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
            _logger.LogError(ex, "JSONL file test failed");
            return Task.FromResult(false);
        }
    }

    public async Task<SchemaDetectionResult> DetectSchemaAsync(Connector connector, CancellationToken cancellationToken)
    {
        try
        {
            var config = ParseConfig(connector.ConfigJson);

            using var reader = new StreamReader(config.FilePath);
            var firstLine = await reader.ReadLineAsync(cancellationToken);

            if (string.IsNullOrWhiteSpace(firstLine))
            {
                return new SchemaDetectionResult
                {
                    Success = false,
                    ErrorMessage = "File is empty",
                    DetectedAt = DateTimeOffset.UtcNow
                };
            }

            var firstRow = JsonSerializer.Deserialize<Dictionary<string, object?>>(firstLine);

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
            _logger.LogError(ex, "Schema detection failed for JSONL");
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

    private JsonLinesConfig ParseConfig(string configJson)
    {
        return JsonSerializer.Deserialize<JsonLinesConfig>(configJson)
            ?? throw new InvalidOperationException("Invalid JSONL configuration");
    }

    private class JsonLinesConfig
    {
        public string FilePath { get; set; } = string.Empty;
        /// <summary>Key into <see cref="AzureBlobDataReader._streamRegistry"/> for cloud-streamed blobs.</summary>
        public Guid? StreamRegistryKey { get; set; }
    }
}
