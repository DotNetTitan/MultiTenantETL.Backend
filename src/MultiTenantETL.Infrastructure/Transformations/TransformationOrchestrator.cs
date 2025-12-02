using System.Diagnostics;
using Microsoft.Extensions.Logging;
using MultiTenantETL.Application.Connectors.DataReaders;
using MultiTenantETL.Application.Transformations;

namespace MultiTenantETL.Infrastructure.Transformations;

/// <summary>
/// Orchestrates the execution of multiple transformations on a batch
/// </summary>
public class TransformationOrchestrator : ITransformationOrchestrator
{
    private readonly ITransformationService _transformationService;
    private readonly IEnumerable<ITransformationProcessor> _processors;
    private readonly ILogger<TransformationOrchestrator> _logger;

    public TransformationOrchestrator(
        ITransformationService transformationService,
        IEnumerable<ITransformationProcessor> processors,
        ILogger<TransformationOrchestrator> logger)
    {
        _transformationService = transformationService;
        _processors = processors;
        _logger = logger;
    }

    public async Task<TransformationOrchestrationResult> ApplyTransformationsAsync(
        ReadBatch batch,
        Guid pipelineId,
        TransformationPolicy policy,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new TransformationOrchestrationResult
        {
            BatchId = batch.BatchId,
            InitialRowCount = batch.RowCount,
            Success = true
        };

        try
        {
            // NOTE: Transformations are now embedded in field mappings
            // This orchestrator is kept for backward compatibility and global transformations
            // Field-level transformations are handled by FieldMappingService
            
            _logger.LogInformation(
                "TransformationOrchestrator called for pipeline {PipelineId} - transformations now handled in field mappings",
                pipelineId);

            // Return batch unchanged - transformations applied in field mapping phase
            result.TransformedBatch = batch;
            result.FinalRowCount = batch.RowCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Orchestration failed for batch {BatchId}", batch.BatchId);
            result.Success = false;
            result.ErrorMessage = $"Orchestration failed: {ex.Message}";
            result.TransformedBatch = batch;
            result.FinalRowCount = batch.RowCount;
        }
        finally
        {
            stopwatch.Stop();
            result.TotalExecutionTime = stopwatch.Elapsed;
        }

        return result;
    }

    private async Task<TransformationStepResult> ApplyTransformationAsync(
        ReadBatch batch,
        Domain.Entities.Transformation transformation,
        TransformationPolicy policy,
        CancellationToken cancellationToken)
    {
        // Find the appropriate processor for this transformation type
        var processor = _processors.FirstOrDefault(p => 
            p.TransformationType.Equals(transformation.Type, StringComparison.OrdinalIgnoreCase));

        if (processor == null)
        {
            throw new NotSupportedException($"No processor found for transformation type '{transformation.Type}'");
        }

        // Execute the transformation
        var result = await processor.ProcessBatchAsync(batch, transformation, cancellationToken);

        return new TransformationStepResult
        {
            TransformationId = transformation.Id,
            TransformationType = transformation.Type,
            Order = 0, // Order is now managed in field mappings
            Result = result
        };
    }
}
