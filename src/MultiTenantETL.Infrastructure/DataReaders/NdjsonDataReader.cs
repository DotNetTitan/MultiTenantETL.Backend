using System.Text.Json;
using Microsoft.Extensions.Options;
using MultiTenantETL.Application.Connectors.DataReaders;
using MultiTenantETL.Domain.Entities;
using MultiTenantETL.Infrastructure.Configuration;

namespace MultiTenantETL.Infrastructure.DataReaders;

/// <summary>
/// Reader for NDJSON (Newline-Delimited JSON) files - optimized for streaming large datasets
/// Each line is a separate JSON object, enabling true streaming without loading entire file into memory
/// </summary>
public class NdjsonDataReader : IDataReader
{
    private readonly EtlSettings _settings;

    public NdjsonDataReader(IOptions<EtlSettings> settings)
    {
        _settings = settings.Value;
    }

    public string ConnectorType => "NDJSON";

    public async Task<DataReadResult> ReadAsync(Connector connector, CancellationToken cancellationToken = default)
    {
        var config = ParseConfig(connector.ConfigJson);
        var result = new DataReadResult();

        using var reader = new StreamReader(config.FilePath);
        
        string? line;
        while ((line = await reader.ReadLineAsync(cancellationToken)) != null)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            try
            {
                var obj = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(line);
                if (obj != null)
                {
                    result.Rows.Add(ConvertJsonElement(obj));
                }
            }
            catch (JsonException)
            {
                // Skip invalid JSON lines
                continue;
            }
        }

        result.TotalRows = result.Rows.Count;
        
        if (result.Rows.Count > 0)
        {
            result.Schema = BuildSchema(result.Rows[0]);
        }

        return result;
    }

    public async IAsyncEnumerable<List<Dictionary<string, object?>>> ReadBatchesAsync(
        Connector connector,
        int batchSize = 0,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var config = ParseConfig(connector.ConfigJson);
        var effectiveBatchSize = batchSize > 0 ? batchSize : _settings.FileBatchSize;

        using var reader = new StreamReader(config.FilePath);
        var batch = new List<Dictionary<string, object?>>(effectiveBatchSize);

        string? line;
        while ((line = await reader.ReadLineAsync(cancellationToken)) != null)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            // Parse JSON outside of yield context
            Dictionary<string, JsonElement>? obj = null;
            try
            {
                obj = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(line);
            }
            catch (JsonException)
            {
                // Skip invalid JSON lines
                continue;
            }

            if (obj != null)
            {
                batch.Add(ConvertJsonElement(obj));

                if (batch.Count >= effectiveBatchSize)
                {
                    yield return batch;
                    batch = new List<Dictionary<string, object?>>(effectiveBatchSize);
                }
            }
        }

        if (batch.Count > 0)
        {
            yield return batch;
        }
    }

    public Task<ConnectionTestResult> TestConnectionAsync(Connector connector)
    {
        try
        {
            var config = ParseConfig(connector.ConfigJson);
            
            if (!File.Exists(config.FilePath))
            {
                return Task.FromResult(new ConnectionTestResult
                {
                    IsSuccessful = false,
                    Message = "File not found",
                    ErrorDetails = $"The file '{config.FilePath}' does not exist"
                });
            }

            return Task.FromResult(new ConnectionTestResult
            {
                IsSuccessful = true,
                Message = "File found and accessible"
            });
        }
        catch (Exception ex)
        {
            return Task.FromResult(new ConnectionTestResult
            {
                IsSuccessful = false,
                Message = "File access failed",
                ErrorDetails = ex.Message
            });
        }
    }

    public async Task<SchemaDetectionResult> DetectSchemaAsync(Connector connector)
    {
        try
        {
            var config = ParseConfig(connector.ConfigJson);
            
            using var reader = new StreamReader(config.FilePath);
            
            // Read first non-empty line to detect schema
            string? line;
            while ((line = await reader.ReadLineAsync()) != null)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

                var obj = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(line);
                if (obj != null)
                {
                    var row = ConvertJsonElement(obj);
                    
                    return new SchemaDetectionResult
                    {
                        IsSuccessful = true,
                        Schema = BuildSchema(row)
                    };
                }
            }

            return new SchemaDetectionResult
            {
                IsSuccessful = false,
                ErrorMessage = "No valid JSON objects found in file"
            };
        }
        catch (Exception ex)
        {
            return new SchemaDetectionResult
            {
                IsSuccessful = false,
                ErrorMessage = ex.Message
            };
        }
    }

    private NdjsonConfig ParseConfig(string configJson)
    {
        return JsonSerializer.Deserialize<NdjsonConfig>(configJson) 
            ?? throw new InvalidOperationException("Invalid NDJSON configuration");
    }

    private Dictionary<string, object?> ConvertJsonElement(Dictionary<string, JsonElement> source)
    {
        var result = new Dictionary<string, object?>();
        
        foreach (var kvp in source)
        {
            result[kvp.Key] = kvp.Value.ValueKind switch
            {
                JsonValueKind.String => kvp.Value.GetString(),
                JsonValueKind.Number => kvp.Value.GetDecimal(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Null => null,
                _ => kvp.Value.ToString()
            };
        }

        return result;
    }

    private SchemaInfo BuildSchema(Dictionary<string, object?> sampleRow)
    {
        var schema = new SchemaInfo();
        
        foreach (var kvp in sampleRow)
        {
            schema.Fields.Add(new FieldDefinition
            {
                Name = kvp.Key,
                DataType = kvp.Value?.GetType().Name ?? "string",
                IsNullable = true
            });
        }

        return schema;
    }

    private class NdjsonConfig
    {
        public string FilePath { get; set; } = string.Empty;
    }
}
