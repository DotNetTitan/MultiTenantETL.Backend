using Jint;
using Jint.Native;
using Microsoft.Extensions.Logging;
using MultiTenantETL.Infrastructure.Transformations.Core;
using System.Text.Json;

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
        return ApplyTransformation(value, transformation, sourceFields, null);
    }

    public object? ApplyTransformation(object? value, TransformationConfig transformation, List<string> sourceFields, Dictionary<string, object?>? row)
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
                "Script" => ApplyScript(value, config, sourceFields, row),
                _ => value
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error applying transformation {Type}", transformation.Type);
            return value;
        }
    }

    private object? ApplyScript(object? value, JsonElement config, List<string> sourceFields, Dictionary<string, object?>? row)
    {
        var script = config.TryGetProperty("script", out var scriptProp) ? scriptProp.GetString() : "";

        if (string.IsNullOrEmpty(script))
        {
            _logger.LogWarning("Script transformation has empty script");
            return value;
        }

        try
        {
            // Create Jint engine with security constraints
            var engine = new Engine(options =>
            {
                options.LimitRecursion(100);
                options.TimeoutInterval(TimeSpan.FromMilliseconds(5000));
                options.MaxStatements(10000);
            });

            // Set the current field value
            engine.SetValue("value", value);

            // Set source field names for reference
            engine.SetValue("sourceFields", sourceFields);

            // If we have the full row context, expose it
            if (row != null)
            {
                engine.SetValue("row", row);
            }
            else
            {
                // Create a minimal row object with just the source field(s)
                var minimalRow = new Dictionary<string, object?>();
                if (sourceFields.Count == 1)
                {
                    minimalRow[sourceFields[0]] = value;
                }
                else if (value is List<object?> values && values.Count == sourceFields.Count)
                {
                    for (int i = 0; i < sourceFields.Count; i++)
                    {
                        minimalRow[sourceFields[i]] = values[i];
                    }
                }
                engine.SetValue("row", minimalRow);
            }

            // Execute the script
            var result = engine.Evaluate(script);

            // Convert result back to .NET type
            return ConvertJsValue(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing script transformation: {Script}", script);
            return value; // Return original value on error
        }
    }

    private object? ConvertJsValue(JsValue value)
    {
        if (value.IsNull() || value.IsUndefined())
            return null;

        if (value.IsBoolean())
            return value.AsBoolean();

        if (value.IsNumber())
        {
            var num = value.AsNumber();
            // Try to preserve integer types
            if (num % 1 == 0 && num >= int.MinValue && num <= int.MaxValue)
                return (int)num;
            if (num % 1 == 0 && num >= long.MinValue && num <= long.MaxValue)
                return (long)num;
            return num;
        }

        if (value.IsString())
            return value.AsString();

        if (value.IsDate())
            return value.AsDate().ToDateTime();

        if (value.IsArray())
        {
            var array = value.AsArray();
            var list = new List<object?>();
            for (uint i = 0; i < array.Length; i++)
            {
                list.Add(ConvertJsValue(array.Get(i.ToString())));
            }
            return list;
        }

        if (value.IsObject())
        {
            var dict = new Dictionary<string, object?>();
            foreach (var property in value.AsObject().GetOwnProperties())
            {
                var key = property.Key.ToString();
                dict[key] = ConvertJsValue(property.Value.Value);
            }
            return dict;
        }

        return value.ToString();
    }
}
