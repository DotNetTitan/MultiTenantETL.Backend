using System.Text.Json;
using Microsoft.Extensions.Logging;
using MultiTenantETL.Application.Connectors.DataReaders;
using MultiTenantETL.Application.Orchestration;
using MultiTenantETL.Application.Transformations;
using MultiTenantETL.Infrastructure.Transformations.FieldProcessors;
using MultiTenantETL.Domain.Entities;

namespace MultiTenantETL.Infrastructure.Orchestration;

public class FieldMappingService : IFieldMappingService
{
    private readonly ILogger<FieldMappingService> _logger;
    private readonly IFieldTransformationProcessor _fieldProcessor;
    private readonly IEnumerable<ITransformationProcessor> _batchProcessors;

    public FieldMappingService(
        ILogger<FieldMappingService> logger,
        IFieldTransformationProcessor fieldProcessor,
        IEnumerable<ITransformationProcessor> batchProcessors)
    {
        _logger = logger;
        _fieldProcessor = fieldProcessor;
        _batchProcessors = batchProcessors;
    }

    public ReadBatch ApplyFieldMappings(ReadBatch batch, string fieldMappingsJson)
    {
        if (string.IsNullOrEmpty(fieldMappingsJson) || fieldMappingsJson == "[]")
        {
            return batch; // No mappings, return as-is
        }

        try
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var mappings = JsonSerializer.Deserialize<List<FieldMapping>>(fieldMappingsJson, options);
            if (mappings == null || mappings.Count == 0)
            {
                return batch;
            }

            // HYBRID APPROACH: Separate simple vs complex mappings
            var simpleMappings = mappings.Where(m => m.SourceFields.Count == 1).OrderBy(m => m.Order).ToList();
            var complexMappings = mappings.Where(m => m.SourceFields.Count > 1).OrderBy(m => m.Order).ToList();

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
                                transformedValue = ApplyFieldTransformation(transformedValue, trans, sourceField);
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
            if (complexMappings.Any())
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
                                var transformationConfig = new Transformations.FieldProcessors.TransformationConfig
                                {
                                    Id = trans.Id,
                                    Type = trans.Type,
                                    Config = trans.Config,
                                    Order = trans.Order,
                                    IsEnabled = trans.IsEnabled
                                };
                                result = _fieldProcessor.ApplyTransformation(result, transformationConfig, mapping.SourceFields);
                            }
                        }

                        row[mapping.DestinationField] = result;
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

    private ReadBatch ApplyBatchTransformation(ReadBatch batch, string fieldName, TransformationDto transformation)
    {
        try
        {
            // Find appropriate batch processor
            var processor = _batchProcessors.FirstOrDefault(p =>
                p.TransformationType.Equals(transformation.Type, StringComparison.OrdinalIgnoreCase));

            if (processor == null)
            {
                _logger.LogWarning("No batch processor found for type {Type}, skipping", transformation.Type);
                return batch;
            }

            // Parse transformation ID - use TryParse for robustness with legacy or frontend-generated IDs
            // If parsing fails, generate a new GUID since the ID is only used for tracking/logging
            if (!Guid.TryParse(transformation.Id, out var transformationId))
            {
                _logger.LogDebug("Transformation ID '{Id}' is not a valid GUID, generating new ID for execution", transformation.Id);
                transformationId = Guid.NewGuid();
            }

            // Create transformation entity for processor
            var transformationEntity = new Transformation
            {
                Id = transformationId,
                TenantId = Guid.Empty, // Not needed for processing
                Name = transformation.Type,
                Type = transformation.Type,
                ConfigJson = transformation.Config?.ToString() ?? "{}",
                CreatedAt = DateTime.UtcNow,
                CreatedBy = Guid.Empty
            };

            // Apply transformation synchronously (processors are fast)
            var result = processor.ProcessBatchAsync(batch, transformationEntity, CancellationToken.None).GetAwaiter().GetResult();

            return new ReadBatch
            {
                BatchId = batch.BatchId,
                Rows = result.TransformedRows,
                RowCount = result.TransformedRows.Count
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error applying batch transformation {Type} to field {Field}", transformation.Type, fieldName);
            return batch;
        }
    }

    /// <summary>
    /// Applies a transformation to a single field value.
    /// This is used for per-field transformations in field mappings.
    /// If the transformation fails, it logs the error and returns the original value.
    /// </summary>
    private object? ApplyFieldTransformation(object? value, TransformationDto transformation, string fieldName)
    {
        try
        {
            var transformationConfig = new Transformations.FieldProcessors.TransformationConfig
            {
                Id = transformation.Id,
                Type = transformation.Type,
                Config = transformation.Config,
                Order = transformation.Order,
                IsEnabled = transformation.IsEnabled
            };

            return _fieldProcessor.ApplyTransformation(value, transformationConfig, new List<string> { fieldName });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error applying transformation {Type} to field {Field}, returning original value", 
                transformation.Type, fieldName);
            return value; // Return original value on error
        }
    }

    private ReadBatch RenameFieldInBatch(ReadBatch batch, string oldName, string newName)
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



    private class FieldMapping
    {
        public string Id { get; set; } = string.Empty;
        public int Order { get; set; }
        public List<string> SourceFields { get; set; } = new();
        public List<TransformationDto> Transformations { get; set; } = new();
        public string DestinationField { get; set; } = string.Empty;
    }

    private class TransformationDto
    {
        public string Id { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public JsonElement? Config { get; set; }
        public int Order { get; set; }
        public bool IsEnabled { get; set; } = true;
    }


}
