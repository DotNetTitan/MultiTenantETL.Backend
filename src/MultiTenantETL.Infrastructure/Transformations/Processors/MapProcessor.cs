using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using MultiTenantETL.Application.Connectors.DataReaders;
using MultiTenantETL.Application.Transformations;
using MultiTenantETL.Domain.Entities;
using MultiTenantETL.Infrastructure.Configuration;

namespace MultiTenantETL.Infrastructure.Transformations.Processors;

/// <summary>
/// Maps field names from source to destination
/// Supports: rename, copy, constant values, and field removal
/// </summary>
public class MapProcessor : ITransformationProcessor
{
    private readonly ILogger<MapProcessor> _logger;

    public string TransformationType => "Map";

    public MapProcessor(ILogger<MapProcessor> logger)
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
            var mappedRows = new List<Dictionary<string, object?>>(batch.Rows.Count);

            for (int i = 0; i < batch.Rows.Count; i++)
            {
                var row = batch.Rows[i];
                
                try
                {
                    var mappedRow = ApplyMapping(row, config);
                    mappedRows.Add(mappedRow);
                }
                catch (Exception ex)
                {
                    result.RowsWithErrors++;
                    result.Errors.Add(new TransformationError
                    {
                        RowIndex = i,
                        Message = ex.Message,
                        ErrorCode = "MAP_ERROR",
                        RowData = row
                    });
                    
                    // Include original row on error
                    mappedRows.Add(row);
                }
            }

            result.TransformedRows = mappedRows;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Map transformation failed for batch {BatchId}", batch.BatchId);
            throw;
        }
        finally
        {
            stopwatch.Stop();
            result.ExecutionTime = stopwatch.Elapsed;
        }

        return Task.FromResult(result);
    }

    private Dictionary<string, object?> ApplyMapping(Dictionary<string, object?> row, MapConfig config)
    {
        var mappedRow = new Dictionary<string, object?>();

        // Apply mappings
        foreach (var mapping in config.Mappings)
        {
            switch (mapping.Type.ToLower())
            {
                case "rename":
                    // Rename: source -> destination
                    if (row.TryGetValue(mapping.Source!, out var value))
                    {
                        mappedRow[mapping.Destination!] = value;
                    }
                    else if (mapping.Required)
                    {
                        throw new InvalidOperationException($"Required field '{mapping.Source}' not found");
                    }
                    break;

                case "copy":
                    // Copy: keep both source and destination
                    if (row.TryGetValue(mapping.Source!, out var copyValue))
                    {
                        mappedRow[mapping.Source!] = copyValue;
                        mappedRow[mapping.Destination!] = copyValue;
                    }
                    break;

                case "constant":
                    // Constant: set destination to a constant value
                    mappedRow[mapping.Destination!] = mapping.Value;
                    break;

                case "keep":
                    // Keep: pass through unchanged
                    if (row.TryGetValue(mapping.Source!, out var keepValue))
                    {
                        mappedRow[mapping.Source!] = keepValue;
                    }
                    break;

                case "remove":
                    // Remove: explicitly exclude field (do nothing)
                    break;

                default:
                    throw new NotSupportedException($"Mapping type '{mapping.Type}' is not supported");
            }
        }

        // Handle unmapped fields based on strategy
        if (config.UnmappedFieldStrategy == "keep")
        {
            foreach (var kvp in row)
            {
                if (!mappedRow.ContainsKey(kvp.Key))
                {
                    mappedRow[kvp.Key] = kvp.Value;
                }
            }
        }

        return mappedRow;
    }

    private MapConfig ParseConfig(string configJson)
    {
        return JsonSerializer.Deserialize<MapConfig>(configJson, JsonSerializerOptionsProvider.Default)
            ?? throw new InvalidOperationException("Invalid map configuration");
    }

    private class MapConfig
    {
        public List<FieldMapping> Mappings { get; set; } = new();
        
        /// <summary>
        /// Strategy for unmapped fields: "keep" or "remove"
        /// </summary>
        public string UnmappedFieldStrategy { get; set; } = "keep";
    }

    private class FieldMapping
    {
        /// <summary>
        /// Type: rename, copy, constant, keep, remove
        /// </summary>
        public string Type { get; set; } = string.Empty;
        
        /// <summary>
        /// Source field name
        /// </summary>
        public string? Source { get; set; }
        
        /// <summary>
        /// Destination field name
        /// </summary>
        public string? Destination { get; set; }
        
        /// <summary>
        /// Constant value (for type=constant)
        /// </summary>
        public string? Value { get; set; }
        
        /// <summary>
        /// Whether this field is required
        /// </summary>
        public bool Required { get; set; }
    }
}
