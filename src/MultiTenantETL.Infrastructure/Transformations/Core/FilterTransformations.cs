using System.Text.Json;

namespace MultiTenantETL.Infrastructure.Transformations.Core;

/// <summary>
/// Core filter/comparison logic shared by batch and field processors
/// </summary>
public static class FilterTransformations
{
    public static bool EvaluateCondition(object? fieldValue, string operatorType, string? compareValue, List<string>? compareValues = null)
    {
        return operatorType.ToLower() switch
        {
            "equals" => CompareEquals(fieldValue, compareValue),
            "not_equals" or "notequals" => !CompareEquals(fieldValue, compareValue),
            "contains" => fieldValue?.ToString()?.Contains(compareValue ?? "", StringComparison.OrdinalIgnoreCase) ?? false,
            "not_contains" or "notcontains" => !(fieldValue?.ToString()?.Contains(compareValue ?? "", StringComparison.OrdinalIgnoreCase) ?? false),
            "starts_with" or "startswith" => fieldValue?.ToString()?.StartsWith(compareValue ?? "", StringComparison.OrdinalIgnoreCase) ?? false,
            "ends_with" or "endswith" => fieldValue?.ToString()?.EndsWith(compareValue ?? "", StringComparison.OrdinalIgnoreCase) ?? false,
            "greater_than" or "greaterthan" => CompareGreaterThan(fieldValue, compareValue),
            "less_than" or "lessthan" => CompareLessThan(fieldValue, compareValue),
            "greater_than_or_equal" => CompareGreaterThanOrEqual(fieldValue, compareValue),
            "less_than_or_equal" => CompareLessThanOrEqual(fieldValue, compareValue),
            "is_null" or "isnull" or "isempty" => fieldValue == null || string.IsNullOrEmpty(fieldValue.ToString()),
            "is_not_null" or "isnotnull" or "isnotempty" => fieldValue != null && !string.IsNullOrEmpty(fieldValue.ToString()),
            "in" => compareValues?.Contains(fieldValue?.ToString() ?? "") ?? false,
            "not_in" => !(compareValues?.Contains(fieldValue?.ToString() ?? "") ?? false),
            _ => throw new NotSupportedException($"Operator '{operatorType}' is not supported")
        };
    }

    public static bool EvaluateCondition(object? fieldValue, JsonElement config)
    {
        var operatorType = config.TryGetProperty("operator", out var opProp) ? opProp.GetString() ?? "" : "";
        var compareValue = config.TryGetProperty("value", out var valProp) ? valProp.GetString() : null;
        
        List<string>? compareValues = null;
        if (config.TryGetProperty("values", out var valuesProp) && valuesProp.ValueKind == JsonValueKind.Array)
        {
            compareValues = valuesProp.EnumerateArray().Select(v => v.GetString() ?? "").ToList();
        }

        return EvaluateCondition(fieldValue, operatorType, compareValue, compareValues);
    }

    private static bool CompareEquals(object? fieldValue, string? compareValue)
    {
        if (fieldValue == null && compareValue == null) return true;
        if (fieldValue == null || compareValue == null) return false;

        return fieldValue.ToString()?.Equals(compareValue, StringComparison.OrdinalIgnoreCase) ?? false;
    }

    private static bool CompareGreaterThan(object? fieldValue, string? compareValue)
    {
        if (fieldValue == null || compareValue == null) return false;

        if (decimal.TryParse(fieldValue.ToString(), out var fieldNum) &&
            decimal.TryParse(compareValue, out var compareNum))
        {
            return fieldNum > compareNum;
        }

        return string.Compare(fieldValue.ToString(), compareValue, StringComparison.Ordinal) > 0;
    }

    private static bool CompareLessThan(object? fieldValue, string? compareValue)
    {
        if (fieldValue == null || compareValue == null) return false;

        if (decimal.TryParse(fieldValue.ToString(), out var fieldNum) &&
            decimal.TryParse(compareValue, out var compareNum))
        {
            return fieldNum < compareNum;
        }

        return string.Compare(fieldValue.ToString(), compareValue, StringComparison.Ordinal) < 0;
    }

    private static bool CompareGreaterThanOrEqual(object? fieldValue, string? compareValue)
    {
        return CompareEquals(fieldValue, compareValue) || CompareGreaterThan(fieldValue, compareValue);
    }

    private static bool CompareLessThanOrEqual(object? fieldValue, string? compareValue)
    {
        return CompareEquals(fieldValue, compareValue) || CompareLessThan(fieldValue, compareValue);
    }
}
