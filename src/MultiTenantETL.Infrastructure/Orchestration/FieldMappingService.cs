using System.Text.Json;
using Microsoft.Extensions.Logging;
using MultiTenantETL.Application.Connectors.DataReaders;
using MultiTenantETL.Application.Orchestration;
using MultiTenantETL.Infrastructure.Configuration;
using MultiTenantETL.Infrastructure.Transformations.FieldProcessors;

namespace MultiTenantETL.Infrastructure.Orchestration;

public class FieldMappingService : IFieldMappingService
{
    private readonly ILogger<FieldMappingService> _logger;
    private readonly IFieldTransformationProcessor _fieldProcessor;

    public FieldMappingService(
        ILogger<FieldMappingService> logger,
        IFieldTransformationProcessor fieldProcessor)
    {
        _logger = logger;
        _fieldProcessor = fieldProcessor;
    }

    public ReadBatch ApplyFieldMappings(ReadBatch batch, string fieldMappingsJson)
    {
        if (string.IsNullOrEmpty(fieldMappingsJson) || fieldMappingsJson == "[]")
        {
            return batch; // No mappings, return as-is
        }

        try
        {
            var mappings = JsonSerializer.Deserialize<List<FieldMapping>>(fieldMappingsJson, JsonSerializerOptionsProvider.Default);
            if (mappings == null || mappings.Count == 0)
            {
                return batch;
            }

            // HYBRID APPROACH: Separate simple vs complex mappings
            var simpleMappings = mappings.Where(m => m.SourceFields.Count == 1).OrderBy(m => m.Order).ToList();
            var complexMappings = mappings.Where(m => m.SourceFields.Count > 1).OrderBy(m => m.Order).ToList();
            
            // Collect all destination fields to know which fields should be preserved
            var allDestinationFields = mappings.Select(m => m.DestinationField).ToHashSet();

            // STEP 1: Process simple mappings (1 source field -> 1 destination field)
            // Apply transformations per-field, not per-batch, to avoid filtering out rows
            foreach (var mapping in simpleMappings)
            {
                var sourceField = mapping.SourceFields[0];

                // Apply transformations to each row's field value individually
                if (mapping.Transformations != null && mapping.Transformations.Count > 0)
                {
                    foreach (var row in batch.Rows)
                    {
                        if (row.TryGetValue(sourceField, out var fieldValue))
                        {
                            object? transformedValue = fieldValue;
                            
                            foreach (var trans in mapping.Transformations.OrderBy(t => t.Order).Where(t => t.IsEnabled))
                            {
                                transformedValue = ApplyFieldTransformation(transformedValue, trans, sourceField, row);
                            }
                            
                            // Store the transformed value in the destination field
                            row[mapping.DestinationField] = transformedValue;
                            
                            // Remove source field if different from destination
                            if (sourceField != mapping.DestinationField)
                            {
                                row.Remove(sourceField);
                            }
                        }
                        else
                        {
                            // Source field doesn't exist, set destination to null
                            row[mapping.DestinationField] = null;
                        }
                    }
                }
                else
                {
                    // No transformations, just rename field if needed
                    if (sourceField != mapping.DestinationField)
                    {
                        batch = RenameFieldInBatch(batch, sourceField, mapping.DestinationField);
                    }
                }
            }

            // STEP 2: Process complex mappings ROW-BY-ROW (flexibility)
            if (complexMappings.Count > 0)
            {
                foreach (var row in batch.Rows)
                {
                    foreach (var mapping in complexMappings)
                    {
                        // Get multiple source values
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

                        // Apply transformations to combined value
                        object? result = values;
                        if (mapping.Transformations != null && mapping.Transformations.Count > 0)
                        {
                            foreach (var trans in mapping.Transformations.OrderBy(t => t.Order).Where(t => t.IsEnabled))
                            {
                                var transformationConfig = new TransformationConfig
                                {
                                    Id = trans.Id,
                                    Type = trans.Type,
                                    Config = trans.Config,
                                    Order = trans.Order,
                                    IsEnabled = trans.IsEnabled
                                };
                                result = _fieldProcessor.ApplyTransformation(result, transformationConfig, mapping.SourceFields, row);
                            }
                        }

                        row[mapping.DestinationField] = result;
                        
                        // Remove source fields that are not used as destination fields elsewhere
                        // This prevents sending unmapped columns to the destination while preserving mapped ones
                        foreach (var sourceField in mapping.SourceFields)
                        {
                            if (sourceField != mapping.DestinationField && !allDestinationFields.Contains(sourceField))
                            {
                                row.Remove(sourceField);
                            }
                        }
                    }
                }
            }

            return batch;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to apply field mappings, using original batch");
            return batch;
        }
    }

    /// <summary>
    /// Applies a transformation to a single field value.
    /// This is used for per-field transformations in field mappings.
    /// If the transformation fails, it logs the error and returns the original value.
    /// </summary>
    private object? ApplyFieldTransformation(object? value, TransformationDto transformation, string fieldName, Dictionary<string, object?>? row = null)
    {
        try
        {
            var transformationConfig = new TransformationConfig
            {
                Id = transformation.Id,
                Type = transformation.Type,
                Config = transformation.Config,
                Order = transformation.Order,
                IsEnabled = transformation.IsEnabled
            };

            return _fieldProcessor.ApplyTransformation(value, transformationConfig, new List<string> { fieldName }, row);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error applying transformation {Type} to field {Field}, returning original value", 
                transformation.Type, fieldName);
            return value; // Return original value on error
        }
    }

    private static ReadBatch RenameFieldInBatch(ReadBatch batch, string oldName, string newName)
    {
        foreach (var row in batch.Rows)
        {
            if (row.TryGetValue(oldName, out var value))
            {
                row[newName] = value;
                if (oldName != newName)
                {
                    row.Remove(oldName);
                }
            }
        }
        return batch;
    }

    private sealed class FieldMapping
    {
        public string Id { get; set; } = string.Empty;
        public int Order { get; set; }
        public List<string> SourceFields { get; set; } = new();
        public List<TransformationDto> Transformations { get; set; } = new();
        public string DestinationField { get; set; } = string.Empty;
    }

    private sealed class TransformationDto
    {
        public string Id { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public JsonElement? Config { get; set; }
        public int Order { get; set; }
        public bool IsEnabled { get; set; } = true;
    }
}
