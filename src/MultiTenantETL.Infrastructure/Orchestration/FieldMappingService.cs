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

            // HYBRID APPROACH: Separate simple vs complex mappings in a single pass
            var simpleMappings = new List<FieldMapping>(mappings.Count);
            var complexMappings = new List<FieldMapping>(mappings.Count);
            var allDestinationFields = new HashSet<string>(mappings.Count);
            
            foreach (var mapping in mappings)
            {
                allDestinationFields.Add(mapping.DestinationField);
                if (mapping.SourceFields.Count == 1)
                {
                    simpleMappings.Add(mapping);
                }
                else
                {
                    complexMappings.Add(mapping);
                }
            }
            
            // Sort once, not per row
            simpleMappings.Sort((a, b) => a.Order.CompareTo(b.Order));
            complexMappings.Sort((a, b) => a.Order.CompareTo(b.Order));

            // STEP 1: Process simple mappings (1 source field -> 1 destination field)
            // Apply transformations per-field, not per-batch, to avoid filtering out rows
            foreach (var mapping in simpleMappings)
            {
                var sourceField = mapping.SourceFields[0];

                // Apply transformations to each row's field value individually
                if (mapping.Transformations != null && mapping.Transformations.Count > 0)
                {
                    // Pre-filter and sort transformations ONCE per mapping, not per row
                    var enabledTransformations = mapping.Transformations
                        .Where(t => t.IsEnabled)
                        .OrderBy(t => t.Order)
                        .ToList();
                    
                    if (enabledTransformations.Count > 0)
                    {
                        foreach (var row in batch.Rows)
                        {
                            if (row.TryGetValue(sourceField, out var fieldValue))
                            {
                                object? transformedValue = fieldValue;
                                
                                foreach (var trans in enabledTransformations)
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
                        // All transformations disabled, just rename field if needed
                        if (sourceField != mapping.DestinationField)
                        {
                            batch = RenameFieldInBatch(batch, sourceField, mapping.DestinationField);
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
                // Pre-compute enabled transformations for each complex mapping ONCE
                var mappingTransformations = new List<(FieldMapping Mapping, List<TransformationDto> EnabledTransformations)>();
                foreach (var mapping in complexMappings)
                {
                    List<TransformationDto> enabled;
                    if (mapping.Transformations != null && mapping.Transformations.Count > 0)
                    {
                        enabled = mapping.Transformations
                            .Where(t => t.IsEnabled)
                            .OrderBy(t => t.Order)
                            .ToList();
                    }
                    else
                    {
                        enabled = new List<TransformationDto>();
                    }
                    mappingTransformations.Add((mapping, enabled));
                }
                
                foreach (var row in batch.Rows)
                {
                    foreach (var (mapping, enabledTransformations) in mappingTransformations)
                    {
                        // Get multiple source values - use capacity hint to avoid resizing
                        var values = new List<object?>(mapping.SourceFields.Count);
                        foreach (var sourceField in mapping.SourceFields)
                        {
                            values.Add(row.TryGetValue(sourceField, out var fieldValue) ? fieldValue : null);
                        }

                        // Apply transformations to combined value
                        object? result = values;
                        foreach (var trans in enabledTransformations)
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
                row.Remove(oldName);
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
