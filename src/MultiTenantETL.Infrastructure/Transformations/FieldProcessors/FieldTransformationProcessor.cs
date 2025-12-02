using System.Text.Json;
using Microsoft.Extensions.Logging;
using MultiTenantETL.Infrastructure.Transformations.Core;

namespace MultiTenantETL.Infrastructure.Transformations.FieldProcessors;

/// <summary>
/// Processes field-level transformations using shared core transformation logic
/// Used for complex field mappings (multiple source fields)
/// </summary>
public class FieldTransformationProcessor : IFieldTransformationProcessor
{
    private readonly ILogger<FieldTransformationProcessor> _logger;

    public FieldTransformationProcessor(ILogger<FieldTransformationProcessor> logger)
    {
        _logger = logger;
    }

    public object? ApplyTransformation(object? value, TransformationConfig transformation, List<string> sourceFields)
    {
        try
        {
            if (!transformation.IsEnabled)
            {
                return value;
            }

            if (transformation.Config == null || !transformation.Config.HasValue)
            {
                _logger.LogWarning("Transformation {Type} has no config", transformation.Type);
                return value;
            }

            var config = transformation.Config.Value;
            var stringValue = value?.ToString();

            return transformation.Type switch
            {
                "Trim" => StringTransformations.Trim(stringValue),
                "CaseConvert" => StringTransformations.ApplyCaseConvert(stringValue, config),
                "Substring" => StringTransformations.ApplySubstring(stringValue, config),
                "Replace" => StringTransformations.ApplyReplace(stringValue, config, _logger),
                "Map" => ValueTransformations.ApplyMap(value, config),
                "Filter" => FilterTransformations.EvaluateCondition(value, config) ? value : null, // Return null to filter out
                "Script" => ApplyScript(value, config, sourceFields),
                _ => value
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error applying transformation {Type}", transformation.Type);
            return value;
        }
    }

    private object? ApplyScript(object? value, JsonElement config, List<string> sourceFields)
    {
        // For simple script transformations (like concatenation of multiple fields)
        // Full JavaScript execution would be handled by ScriptProcessor with Jint
        
        // Handle multiple source fields - simple concatenation
        if (value is List<object?> values)
        {
            var separator = " "; // Default separator
            var script = config.TryGetProperty("script", out var scriptProp) ? scriptProp.GetString() : "";
            
            // Try to extract separator from simple join scripts
            if (!string.IsNullOrEmpty(script) && script.Contains("join"))
            {
                var match = System.Text.RegularExpressions.Regex.Match(script, @"join\(['""](.+?)['""]\)");
                if (match.Success)
                {
                    separator = match.Groups[1].Value;
                }
            }
            
            return string.Join(separator, values.Where(v => v != null).Select(v => v.ToString()));
        }

        // For complex scripts with single values, would need full ScriptProcessor
        return value?.ToString();
    }
}
