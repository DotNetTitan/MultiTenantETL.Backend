using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using MultiTenantETL.Application.Connectors.DataReaders;
using MultiTenantETL.Application.Transformations;
using MultiTenantETL.Domain.Entities;
using MultiTenantETL.Infrastructure.Configuration;

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

        return Core.FilterTransformations.EvaluateCondition(fieldValue, config.Operator, config.Value, config.Values);
    }

    private FilterConfig ParseConfig(string configJson)
    {
        return JsonSerializer.Deserialize<FilterConfig>(configJson, JsonSerializerOptionsProvider.Default)
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
