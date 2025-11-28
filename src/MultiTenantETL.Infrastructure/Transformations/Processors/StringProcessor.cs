using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using MultiTenantETL.Application.Connectors.DataReaders;
using MultiTenantETL.Application.Transformations;
using MultiTenantETL.Domain.Entities;

namespace MultiTenantETL.Infrastructure.Transformations.Processors;

/// <summary>
/// Performs string operations on fields
/// Supports: trim, upper, lower, substring, replace, regex_replace, pad_left, pad_right
/// </summary>
public class StringProcessor : ITransformationProcessor
{
    private readonly ILogger<StringProcessor> _logger;

    public string TransformationType => "String";

    public StringProcessor(ILogger<StringProcessor> logger)
    {
        _logger = logger;
    }

    public Task<TransformationResult> ProcessBatchAsync(
        ReadBatch batch,
        Transformation transformation,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new TransformationResult
        {
            BatchId = batch.BatchId,
            RowsProcessed = batch.RowCount
        };

        try
        {
            var config = ParseConfig(transformation.ConfigJson);
            var transformedRows = new List<Dictionary<string, object?>>();

            for (int i = 0; i < batch.Rows.Count; i++)
            {
                var row = batch.Rows[i];
                
                try
                {
                    var transformedRow = new Dictionary<string, object?>(row);
                    
                    foreach (var field in config.Fields)
                    {
                        if (transformedRow.TryGetValue(field, out var value) && value != null)
                        {
                            transformedRow[field] = ApplyStringOperation(value.ToString() ?? "", config);
                        }
                    }
                    
                    transformedRows.Add(transformedRow);
                }
                catch (Exception ex)
                {
                    result.RowsWithErrors++;
                    result.Errors.Add(new TransformationError
                    {
                        RowIndex = i,
                        Message = ex.Message,
                        ErrorCode = "STRING_ERROR",
                        RowData = row
                    });
                    
                    transformedRows.Add(row);
                }
            }

            result.TransformedRows = transformedRows;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "String transformation failed for batch {BatchId}", batch.BatchId);
            throw;
        }
        finally
        {
            stopwatch.Stop();
            result.ExecutionTime = stopwatch.Elapsed;
        }

        return Task.FromResult(result);
    }

    private string ApplyStringOperation(string value, StringConfig config)
    {
        return config.Operation.ToLower() switch
        {
            "trim" => value.Trim(),
            "trim_start" => value.TrimStart(),
            "trim_end" => value.TrimEnd(),
            "upper" => value.ToUpper(),
            "lower" => value.ToLower(),
            "title_case" => System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(value.ToLower()),
            "substring" => Substring(value, config.Start ?? 0, config.Length),
            "replace" => value.Replace(config.OldValue ?? "", config.NewValue ?? ""),
            "regex_replace" => Regex.Replace(value, config.Pattern ?? "", config.Replacement ?? ""),
            "pad_left" => value.PadLeft(config.TotalWidth ?? value.Length, config.PaddingChar ?? ' '),
            "pad_right" => value.PadRight(config.TotalWidth ?? value.Length, config.PaddingChar ?? ' '),
            "remove_whitespace" => Regex.Replace(value, @"\s+", ""),
            "normalize_whitespace" => Regex.Replace(value.Trim(), @"\s+", " "),
            _ => throw new NotSupportedException($"Operation '{config.Operation}' is not supported")
        };
    }

    private string Substring(string value, int start, int? length)
    {
        if (start < 0 || start >= value.Length)
            return string.Empty;
        
        if (length.HasValue)
        {
            var actualLength = Math.Min(length.Value, value.Length - start);
            return value.Substring(start, actualLength);
        }
        
        return value.Substring(start);
    }

    private StringConfig ParseConfig(string configJson)
    {
        return JsonSerializer.Deserialize<StringConfig>(configJson)
            ?? throw new InvalidOperationException("Invalid string configuration");
    }

    private class StringConfig
    {
        public List<string> Fields { get; set; } = new();
        public string Operation { get; set; } = string.Empty;
        
        // For substring
        public int? Start { get; set; }
        public int? Length { get; set; }
        
        // For replace
        public string? OldValue { get; set; }
        public string? NewValue { get; set; }
        
        // For regex_replace
        public string? Pattern { get; set; }
        public string? Replacement { get; set; }
        
        // For padding
        public int? TotalWidth { get; set; }
        public char? PaddingChar { get; set; }
    }
}
