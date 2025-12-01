using System.Text.Json;
using Microsoft.Extensions.Logging;
using MultiTenantETL.Application.Connectors.DataReaders;
using MultiTenantETL.Application.Orchestration;

namespace MultiTenantETL.Infrastructure.Orchestration;

public class FieldMappingService : IFieldMappingService
{
    private readonly ILogger<FieldMappingService> _logger;

    public FieldMappingService(ILogger<FieldMappingService> logger)
    {
        _logger = logger;
    }

    public ReadBatch ApplyFieldMappings(ReadBatch batch, string fieldMappingsJson)
    {
        if (string.IsNullOrEmpty(fieldMappingsJson) || fieldMappingsJson == "[]")
        {
            return batch; // No mappings, return as-is
        }

        try
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var mappings = JsonSerializer.Deserialize<List<FieldMapping>>(fieldMappingsJson, options);
            if (mappings == null || mappings.Count == 0)
            {
                return batch;
            }

            var mappedBatch = new ReadBatch { BatchId = batch.BatchId };

            foreach (var row in batch.Rows)
            {
                var mappedRow = new Dictionary<string, object?>();

                foreach (var mapping in mappings.OrderBy(m => m.Order))
                {
                    // Get source value(s)
                    object? value = null;
                    
                    if (mapping.SourceFields.Count == 0)
                    {
                        _logger.LogWarning("Mapping has no source fields for destination '{Dest}'", mapping.DestinationField);
                        continue;
                    }
                    else if (mapping.SourceFields.Count == 1)
                    {
                        // Single source field
                        var sourceField = mapping.SourceFields[0];
                        if (!row.TryGetValue(sourceField, out value))
                        {
                            _logger.LogWarning("Source field '{SourceField}' not found in row", sourceField);
                            value = null;
                        }
                    }
                    else
                    {
                        // Multiple source fields - create array
                        var values = new List<object?>();
                        foreach (var sourceField in mapping.SourceFields)
                        {
                            if (row.TryGetValue(sourceField, out var fieldValue))
                            {
                                values.Add(fieldValue);
                            }
                            else
                            {
                                values.Add(null);
                            }
                        }
                        value = values;
                    }

                    // Apply transformations in order
                    if (mapping.Transformations != null && mapping.Transformations.Count > 0)
                    {
                        foreach (var transformation in mapping.Transformations.OrderBy(t => t.Order))
                        {
                            value = ApplyTransformation(value, transformation, mapping.SourceFields);
                        }
                    }

                    mappedRow[mapping.DestinationField] = value;
                }

                mappedBatch.Rows.Add(mappedRow);
                mappedBatch.RowCount++;
            }

            return mappedBatch;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to apply field mappings, using original batch");
            return batch;
        }
    }

    private object? ApplyTransformation(object? value, TransformationConfig transformation, List<string> sourceFields)
    {
        try
        {
            if (transformation.Config == null || !transformation.Config.HasValue)
            {
                _logger.LogWarning("Transformation has no config");
                return value;
            }

            var config = transformation.Config.Value;

            return transformation.Type switch
            {
                "Script" => ApplyScriptTransformation(value, config),
                "Trim" => ApplyTrimTransformation(value, config),
                "Case Convert" => ApplyCaseConvertTransformation(value, config),
                "Substring" => ApplySubstringTransformation(value, config),
                "Replace" => ApplyReplaceTransformation(value, config),
                "Map" => ApplyMapTransformation(value, config),
                "Filter" => ApplyFilterTransformation(value, config),
                _ => value
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error applying transformation {Type}", transformation.Type);
            return value;
        }
    }

    private object? ApplyScriptTransformation(object? value, JsonElement config)
    {
        // For now, just convert to string as a simple implementation
        // Full script execution would require Jint or similar
        if (value == null) return null;
        return value.ToString();
    }

    private object? ApplyTrimTransformation(object? value, JsonElement config)
    {
        if (value == null) return null;
        var str = value.ToString();
        if (string.IsNullOrEmpty(str)) return str;

        var trimType = config.TryGetProperty("trimType", out var prop) ? prop.GetString() : "both";
        
        return trimType switch
        {
            "start" => str.TrimStart(),
            "end" => str.TrimEnd(),
            _ => str.Trim()
        };
    }

    private object? ApplyCaseConvertTransformation(object? value, JsonElement config)
    {
        if (value == null) return null;
        var str = value.ToString();
        if (string.IsNullOrEmpty(str)) return str;

        var caseType = config.TryGetProperty("caseType", out var prop) ? prop.GetString() : "uppercase";
        
        return caseType switch
        {
            "lowercase" => str.ToLower(),
            "uppercase" => str.ToUpper(),
            "titlecase" => System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(str.ToLower()),
            "camelcase" => ToCamelCase(str),
            _ => str
        };
    }

    private object? ApplySubstringTransformation(object? value, JsonElement config)
    {
        if (value == null) return null;
        var str = value.ToString();
        if (string.IsNullOrEmpty(str)) return str;

        var start = config.TryGetProperty("start", out var startProp) ? startProp.GetInt32() : 0;
        var length = config.TryGetProperty("length", out var lengthProp) ? lengthProp.GetInt32() : (int?)null;

        if (start >= str.Length) return string.Empty;
        
        return length.HasValue 
            ? str.Substring(start, Math.Min(length.Value, str.Length - start))
            : str.Substring(start);
    }

    private object? ApplyReplaceTransformation(object? value, JsonElement config)
    {
        if (value == null) return null;
        var str = value.ToString();
        if (string.IsNullOrEmpty(str)) return str;

        var searchValue = config.TryGetProperty("searchValue", out var searchProp) ? searchProp.GetString() : "";
        var replaceValue = config.TryGetProperty("replaceValue", out var replaceProp) ? replaceProp.GetString() : "";
        
        if (string.IsNullOrEmpty(searchValue)) return str;

        return str.Replace(searchValue, replaceValue ?? "");
    }

    private object? ApplyMapTransformation(object? value, JsonElement config)
    {
        if (value == null) return null;
        var str = value.ToString();

        if (config.TryGetProperty("mappings", out var mappingsProp) && mappingsProp.ValueKind == JsonValueKind.Array)
        {
            foreach (var mapping in mappingsProp.EnumerateArray())
            {
                var from = mapping.TryGetProperty("from", out var fromProp) ? fromProp.GetString() : "";
                var to = mapping.TryGetProperty("to", out var toProp) ? toProp.GetString() : "";
                
                if (str == from)
                {
                    return to;
                }
            }
        }

        // Return default value if no mapping found
        if (config.TryGetProperty("defaultValue", out var defaultProp))
        {
            return defaultProp.GetString();
        }

        return value;
    }

    private object? ApplyFilterTransformation(object? value, JsonElement config)
    {
        // Filter transformations don't modify values, they're used for row filtering
        // This would be handled at a higher level
        return value;
    }

    private string ToCamelCase(string str)
    {
        if (string.IsNullOrEmpty(str)) return str;
        var words = str.Split(new[] { ' ', '_', '-' }, StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0) return str;
        
        var result = words[0].ToLower();
        for (int i = 1; i < words.Length; i++)
        {
            result += char.ToUpper(words[i][0]) + words[i].Substring(1).ToLower();
        }
        return result;
    }

    private class FieldMapping
    {
        public string Id { get; set; } = string.Empty;
        public int Order { get; set; }
        public List<string> SourceFields { get; set; } = new();
        public List<TransformationConfig> Transformations { get; set; } = new();
        public string DestinationField { get; set; } = string.Empty;
    }

    private class TransformationConfig
    {
        public string TransformationId { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public JsonElement? Config { get; set; }
        public int Order { get; set; }
    }
}
