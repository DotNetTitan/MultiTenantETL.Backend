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
            // Load transformations for this pipeline
            var transformations = await _transformationService.GetByPipelineIdAsync(pipelineId, cancellationToken);
            
            if (!transformations.Any())
            {
                _logger.LogInformation("No transformations configured for pipeline {PipelineId}", pipelineId);
                result.TransformedBatch = batch;
                result.FinalRowCount = batch.RowCount;
                return result;
            }

            // Sort transformations by order
            var orderedTransformations = transformations.OrderBy(t => t.Order).ToList();
            
            _logger.LogInformation(
                "Applying {Count} transformations to batch {BatchId} with policy {Policy}",
                orderedTransformations.Count,
                batch.BatchId,
                policy);

            // Apply transformations sequentially
            var currentBatch = batch;
            
            foreach (var transformation in orderedTransformations)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    result.Success = false;
                    result.ErrorMessage = "Operation cancelled";
                    break;
                }

                try
                {
                    var stepResult = await ApplyTransformationAsync(
                        currentBatch,
                        transformation,
                        policy,
                        cancellationToken);

                    result.StepResults.Add(stepResult);
                    result.TotalRowsFiltered += stepResult.Result.RowsFiltered;
                    result.TotalRowsWithErrors += stepResult.Result.RowsWithErrors;

                    // Check if we should continue based on policy
                    if (stepResult.Result.RowsWithErrors > 0 && policy == TransformationPolicy.FailFast)
                    {
                        result.Success = false;
                        result.ErrorMessage = $"Transformation '{transformation.Name}' failed with {stepResult.Result.RowsWithErrors} errors (FailFast policy)";
                        break;
                    }

                    // Update current batch for next transformation
                    currentBatch = new ReadBatch
                    {
                        BatchId = batch.BatchId,
                        Rows = stepResult.Result.TransformedRows,
                        RowCount = stepResult.Result.TransformedRows.Count
                    };

                    _logger.LogInformation(
                        "Transformation '{Name}' completed: {Processed} processed, {Filtered} filtered, {Errors} errors, {Time}ms",
                        transformation.Name,
                        stepResult.Result.RowsProcessed,
                        stepResult.Result.RowsFiltered,
                        stepResult.Result.RowsWithErrors,
                        stepResult.Result.ExecutionTime.TotalMilliseconds);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Transformation '{Name}' failed", transformation.Name);
                    
                    if (policy == TransformationPolicy.FailFast)
                    {
                        result.Success = false;
                        result.ErrorMessage = $"Transformation '{transformation.Name}' threw exception: {ex.Message}";
                        break;
                    }
                    
                    // Log error but continue with next transformation
                    result.StepResults.Add(new TransformationStepResult
                    {
                        TransformationId = transformation.Id,
                        TransformationType = transformation.Type,
                        Order = transformation.Order,
                        Result = new TransformationResult
                        {
                            BatchId = batch.BatchId,
                            RowsProcessed = currentBatch.RowCount,
                            RowsWithErrors = currentBatch.RowCount,
                            Errors = new List<TransformationError>
                            {
                                new()
                                {
                                    RowIndex = -1,
                                    Message = ex.Message,
                                    ErrorCode = "TRANSFORMATION_EXCEPTION"
                                }
                            }
                        }
                    });
                }
            }

            result.TransformedBatch = currentBatch;
            result.FinalRowCount = currentBatch.RowCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Orchestration failed for batch {BatchId}", batch.BatchId);
            result.Success = false;
            result.ErrorMessage = $"Orchestration failed: {ex.Message}";
            result.TransformedBatch = batch; // Return original batch on failure
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
            Order = transformation.Order,
            Result = result
        };
    }
}
