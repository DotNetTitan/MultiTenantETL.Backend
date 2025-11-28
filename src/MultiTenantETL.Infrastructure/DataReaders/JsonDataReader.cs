using System.Text.Json;
using Microsoft.Extensions.Options;
using MultiTenantETL.Application.Connectors.DataReaders;
using MultiTenantETL.Domain.Entities;
using MultiTenantETL.Infrastructure.Configuration;

namespace MultiTenantETL.Infrastructure.DataReaders;

public class JsonDataReader : IDataReader
{
    private readonly EtlSettings _settings;

    public JsonDataReader(IOptions<EtlSettings> settings)
    {
        _settings = settings.Value;
    }

    public string ConnectorType => "JSON";

    public async Task<DataReadResult> ReadAsync(Connector connector, CancellationToken cancellationToken = default)
    {
        var config = ParseConfig(connector.ConfigJson);
        var result = new DataReadResult();

        var jsonContent = await File.ReadAllTextAsync(config.FilePath, cancellationToken);
        
        if (config.IsArray)
        {
            var array = JsonSerializer.Deserialize<List<Dictionary<string, JsonElement>>>(jsonContent);
            if (array != null)
            {
                foreach (var item in array)
                {
                    result.Rows.Add(ConvertJsonElement(item));
                }
            }
        }
        else
        {
            var obj = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(jsonContent);
            if (obj != null)
            {
                result.Rows.Add(ConvertJsonElement(obj));
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

        if (config.IsArray)
        {
            // Use JsonSerializer.DeserializeAsyncEnumerable for TRUE streaming of JSON arrays
            // This reads one object at a time without loading entire file into memory (.NET 6+)
            await using var fileStream = File.OpenRead(config.FilePath);
            var batch = new List<Dictionary<string, object?>>(effectiveBatchSize);

            await foreach (var element in JsonSerializer.DeserializeAsyncEnumerable<Dictionary<string, JsonElement>>(
                fileStream, cancellationToken: cancellationToken))
            {
                if (element != null)
                {
                    batch.Add(ConvertJsonElement(element));

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
        else
        {
            // Single object - use regular deserialization
            await using var fileStream = File.OpenRead(config.FilePath);
            var element = await JsonSerializer.DeserializeAsync<Dictionary<string, JsonElement>>(
                fileStream, cancellationToken: cancellationToken);

            if (element != null)
            {
                yield return new List<Dictionary<string, object?>> { ConvertJsonElement(element) };
            }
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
            var jsonContent = await File.ReadAllTextAsync(config.FilePath);
            
            Dictionary<string, JsonElement>? firstItem = null;
            
            if (config.IsArray)
            {
                var array = JsonSerializer.Deserialize<List<Dictionary<string, JsonElement>>>(jsonContent);
                firstItem = array?.FirstOrDefault();
            }
            else
            {
                firstItem = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(jsonContent);
            }

            if (firstItem == null)
            {
                return new SchemaDetectionResult
                {
                    IsSuccessful = false,
                    ErrorMessage = "No data found to detect schema"
                };
            }

            var row = ConvertJsonElement(firstItem);
            
            return new SchemaDetectionResult
            {
                IsSuccessful = true,
                Schema = BuildSchema(row)
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

    private JsonConfig ParseConfig(string configJson)
    {
        return JsonSerializer.Deserialize<JsonConfig>(configJson) 
            ?? throw new InvalidOperationException("Invalid JSON configuration");
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

    private class JsonConfig
    {
        public string FilePath { get; set; } = string.Empty;
        public bool IsArray { get; set; } = true;
    }
}
