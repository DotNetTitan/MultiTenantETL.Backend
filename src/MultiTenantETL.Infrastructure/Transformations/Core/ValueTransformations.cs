using System.Text.Json;

namespace MultiTenantETL.Infrastructure.Transformations.Core;

/// <summary>
/// Core value mapping transformation logic shared by batch and field processors
/// </summary>
public static class ValueTransformations
{
    /// <summary>
    /// Apply value mapping based on config
    /// </summary>
    public static object? ApplyMap(object? value, JsonElement config)
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
}
