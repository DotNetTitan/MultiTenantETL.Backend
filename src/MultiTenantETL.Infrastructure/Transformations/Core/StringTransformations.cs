using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace MultiTenantETL.Infrastructure.Transformations.Core;

/// <summary>
/// Core string transformation logic shared by batch and field processors
/// </summary>
public static class StringTransformations
{
    public static string? Trim(string? value)
    {
        return value?.Trim();
    }

    public static string? TrimStart(string? value)
    {
        return value?.TrimStart();
    }

    public static string? TrimEnd(string? value)
    {
        return value?.TrimEnd();
    }

    public static string? ToUpper(string? value)
    {
        return value?.ToUpper();
    }

    public static string? ToLower(string? value)
    {
        return value?.ToLower();
    }

    public static string? ToTitleCase(string? value)
    {
        if (string.IsNullOrEmpty(value)) return value;
        return System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(value.ToLower());
    }

    public static string? ToCamelCase(string? value)
    {
        if (string.IsNullOrEmpty(value)) return value;
        var words = value.Split(new[] { ' ', '_', '-' }, StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0) return value;

        var result = words[0].ToLower();
        for (int i = 1; i < words.Length; i++)
        {
            if (words[i].Length > 0)
            {
                result += char.ToUpper(words[i][0]) + words[i].Substring(1).ToLower();
            }
        }
        return result;
    }

    public static string? Substring(string? value, int start, int? length)
    {
        if (string.IsNullOrEmpty(value)) return value;
        if (start < 0 || start >= value.Length) return string.Empty;

        if (length.HasValue)
        {
            var actualLength = Math.Min(length.Value, value.Length - start);
            return value.Substring(start, actualLength);
        }

        return value.Substring(start);
    }

    public static string? Replace(string? value, string oldValue, string newValue)
    {
        if (string.IsNullOrEmpty(value) || string.IsNullOrEmpty(oldValue)) return value;
        return value.Replace(oldValue, newValue ?? "");
    }

    public static string? RegexReplace(string? value, string pattern, string replacement, ILogger? logger = null)
    {
        if (string.IsNullOrEmpty(value) || string.IsNullOrEmpty(pattern)) return value;

        try
        {
            return Regex.Replace(value, pattern, replacement ?? "");
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Invalid regex pattern: {Pattern}", pattern);
            return value;
        }
    }

    public static string? PadLeft(string? value, int totalWidth, char paddingChar = ' ')
    {
        if (string.IsNullOrEmpty(value)) return value;
        return value.PadLeft(totalWidth, paddingChar);
    }

    public static string? PadRight(string? value, int totalWidth, char paddingChar = ' ')
    {
        if (string.IsNullOrEmpty(value)) return value;
        return value.PadRight(totalWidth, paddingChar);
    }

    public static string? RemoveWhitespace(string? value)
    {
        if (string.IsNullOrEmpty(value)) return value;
        return Regex.Replace(value, @"\s+", "");
    }

    public static string? NormalizeWhitespace(string? value)
    {
        if (string.IsNullOrEmpty(value)) return value;
        return Regex.Replace(value.Trim(), @"\s+", " ");
    }

    /// <summary>
    /// Apply string operation based on config
    /// </summary>
    public static string? ApplyOperation(string? value, string operation, JsonElement config, ILogger? logger = null)
    {
        return operation.ToLower() switch
        {
            "trim" => Trim(value),
            "trim_start" => TrimStart(value),
            "trim_end" => TrimEnd(value),
            "upper" => ToUpper(value),
            "lower" => ToLower(value),
            "title_case" => ToTitleCase(value),
            "camelcase" => ToCamelCase(value),
            "substring" => Substring(value, 
                config.TryGetProperty("start", out var s) ? s.GetInt32() : 0,
                config.TryGetProperty("length", out var l) ? l.GetInt32() : (int?)null),
            "replace" => Replace(value,
                config.TryGetProperty("oldValue", out var ov) ? ov.GetString() ?? "" : "",
                config.TryGetProperty("newValue", out var nv) ? nv.GetString() ?? "" : ""),
            "regex_replace" => RegexReplace(value,
                config.TryGetProperty("pattern", out var p) ? p.GetString() ?? "" : "",
                config.TryGetProperty("replacement", out var r) ? r.GetString() ?? "" : "",
                logger),
            "pad_left" => PadLeft(value,
                config.TryGetProperty("totalWidth", out var tw) ? tw.GetInt32() : value?.Length ?? 0,
                config.TryGetProperty("paddingChar", out var pc) && pc.GetString()?.Length > 0 ? pc.GetString()![0] : ' '),
            "pad_right" => PadRight(value,
                config.TryGetProperty("totalWidth", out var tw2) ? tw2.GetInt32() : value?.Length ?? 0,
                config.TryGetProperty("paddingChar", out var pc2) && pc2.GetString()?.Length > 0 ? pc2.GetString()![0] : ' '),
            "remove_whitespace" => RemoveWhitespace(value),
            "normalize_whitespace" => NormalizeWhitespace(value),
            _ => throw new NotSupportedException($"Operation '{operation}' is not supported")
        };
    }

    /// <summary>
    /// Apply case conversion based on config
    /// </summary>
    public static string? ApplyCaseConvert(string? value, JsonElement config)
    {
        var caseType = config.TryGetProperty("caseType", out var prop) ? prop.GetString() : "uppercase";

        return caseType switch
        {
            "lowercase" => ToLower(value),
            "uppercase" => ToUpper(value),
            "titlecase" => ToTitleCase(value),
            "camelcase" => ToCamelCase(value),
            _ => value
        };
    }

    /// <summary>
    /// Apply substring based on config
    /// </summary>
    public static string? ApplySubstring(string? value, JsonElement config)
    {
        var startIndex = config.TryGetProperty("startIndex", out var startProp) ? startProp.GetInt32() : 0;
        var length = config.TryGetProperty("length", out var lengthProp) ? lengthProp.GetInt32() : (int?)null;

        return Substring(value, startIndex, length);
    }

    /// <summary>
    /// Apply replace based on config
    /// </summary>
    public static string? ApplyReplace(string? value, JsonElement config, ILogger? logger = null)
    {
        var findPattern = config.TryGetProperty("findPattern", out var searchProp) ? searchProp.GetString() : "";
        var replaceWith = config.TryGetProperty("replaceWith", out var replaceProp) ? replaceProp.GetString() : "";
        var useRegex = config.TryGetProperty("useRegex", out var regexProp) && regexProp.GetBoolean();

        if (string.IsNullOrEmpty(findPattern)) return value;

        return useRegex
            ? RegexReplace(value, findPattern, replaceWith ?? "", logger)
            : Replace(value, findPattern, replaceWith ?? "");
    }
}
