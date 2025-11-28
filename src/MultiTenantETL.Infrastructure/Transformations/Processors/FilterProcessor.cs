using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using MultiTenantETL.Application.Connectors.DataReaders;
using MultiTenantETL.Application.Transformations;
using MultiTenantETL.Domain.Entities;

namespace MultiTenantETL.Infrastructure.Transformations.Processors;

/// <summary>
/// Filters rows based on conditions
/// Supports: equals, not_equals, contains, starts_with, ends_with, greater_than, less_than, is_null, is_not_null
/// </summary>
public class FilterProcessor : ITransformationProcessor
{
    private readonly ILogger<FilterProcessor> _logger;

    public string TransformationType => "Filter";

    public FilterProcessor(ILogger<FilterProcessor> logger)
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
            var filteredRows = new List<Dictionary<string, object?>>();

            for (int i = 0; i < batch.Rows.Count; i++)
            {
                var row = batch.Rows[i];
                
                try
                {
                    if (EvaluateCondition(row, config))
                    {
                        filteredRows.Add(row);
                    }
                    else
                    {
                        result.RowsFiltered++;
                    }
                }
                catch (Exception ex)
                {
                    result.RowsWithErrors++;
                    result.Errors.Add(new TransformationError
                    {
                        RowIndex = i,
                        Message = ex.Message,
                        FieldName = config.Field,
                        ErrorCode = "FILTER_ERROR",
                        RowData = row
                    });
                }
            }

            result.TransformedRows = filteredRows;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Filter transformation failed for batch {BatchId}", batch.BatchId);
            throw;
        }
        finally
        {
            stopwatch.Stop();
            result.ExecutionTime = stopwatch.Elapsed;
        }

        return Task.FromResult(result);
    }

    private bool EvaluateCondition(Dictionary<string, object?> row, FilterConfig config)
    {
        if (!row.TryGetValue(config.Field, out var fieldValue))
        {
            // Field doesn't exist
            return config.Operator == "is_null";
        }

        return config.Operator.ToLower() switch
        {
            "equals" => CompareEquals(fieldValue, config.Value),
            "not_equals" => !CompareEquals(fieldValue, config.Value),
            "contains" => fieldValue?.ToString()?.Contains(config.Value ?? "", StringComparison.OrdinalIgnoreCase) ?? false,
            "starts_with" => fieldValue?.ToString()?.StartsWith(config.Value ?? "", StringComparison.OrdinalIgnoreCase) ?? false,
            "ends_with" => fieldValue?.ToString()?.EndsWith(config.Value ?? "", StringComparison.OrdinalIgnoreCase) ?? false,
            "greater_than" => CompareGreaterThan(fieldValue, config.Value),
            "less_than" => CompareLessThan(fieldValue, config.Value),
            "greater_than_or_equal" => CompareGreaterThanOrEqual(fieldValue, config.Value),
            "less_than_or_equal" => CompareLessThanOrEqual(fieldValue, config.Value),
            "is_null" => fieldValue == null,
            "is_not_null" => fieldValue != null,
            "in" => config.Values?.Contains(fieldValue?.ToString() ?? "") ?? false,
            "not_in" => !(config.Values?.Contains(fieldValue?.ToString() ?? "") ?? false),
            _ => throw new NotSupportedException($"Operator '{config.Operator}' is not supported")
        };
    }

    private bool CompareEquals(object? fieldValue, string? compareValue)
    {
        if (fieldValue == null && compareValue == null) return true;
        if (fieldValue == null || compareValue == null) return false;
        
        return fieldValue.ToString()?.Equals(compareValue, StringComparison.OrdinalIgnoreCase) ?? false;
    }

    private bool CompareGreaterThan(object? fieldValue, string? compareValue)
    {
        if (fieldValue == null || compareValue == null) return false;
        
        if (decimal.TryParse(fieldValue.ToString(), out var fieldNum) &&
            decimal.TryParse(compareValue, out var compareNum))
        {
            return fieldNum > compareNum;
        }
        
        return string.Compare(fieldValue.ToString(), compareValue, StringComparison.Ordinal) > 0;
    }

    private bool CompareLessThan(object? fieldValue, string? compareValue)
    {
        if (fieldValue == null || compareValue == null) return false;
        
        if (decimal.TryParse(fieldValue.ToString(), out var fieldNum) &&
            decimal.TryParse(compareValue, out var compareNum))
        {
            return fieldNum < compareNum;
        }
        
        return string.Compare(fieldValue.ToString(), compareValue, StringComparison.Ordinal) < 0;
    }

    private bool CompareGreaterThanOrEqual(object? fieldValue, string? compareValue)
    {
        return CompareEquals(fieldValue, compareValue) || CompareGreaterThan(fieldValue, compareValue);
    }

    private bool CompareLessThanOrEqual(object? fieldValue, string? compareValue)
    {
        return CompareEquals(fieldValue, compareValue) || CompareLessThan(fieldValue, compareValue);
    }

    private FilterConfig ParseConfig(string configJson)
    {
        return JsonSerializer.Deserialize<FilterConfig>(configJson)
            ?? throw new InvalidOperationException("Invalid filter configuration");
    }

    private class FilterConfig
    {
        public string Field { get; set; } = string.Empty;
        public string Operator { get; set; } = string.Empty;
        public string? Value { get; set; }
        public List<string>? Values { get; set; } // For 'in' and 'not_in' operators
    }
}
