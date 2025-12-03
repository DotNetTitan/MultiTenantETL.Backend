using System.Diagnostics;
using Microsoft.Extensions.Logging;
using MultiTenantETL.Application.Connectors.DataReaders;
using MultiTenantETL.Application.Transformations;

namespace MultiTenantETL.Infrastructure.Transformations;

/// <summary>
/// Orchestrates the execution of multiple transformations on a batch.
/// NOTE: Transformations are now embedded in field mappings and processed by FieldMappingService.
/// This orchestrator is kept for backward compatibility and potential future global transformations.
/// </summary>
public class TransformationOrchestrator : ITransformationOrchestrator
{
    private readonly ILogger<TransformationOrchestrator> _logger;

    public TransformationOrchestrator(
        ITransformationService transformationService,
        IEnumerable<ITransformationProcessor> processors,
        ILogger<TransformationOrchestrator> logger)
    {
        // Dependencies kept for backward compatibility with DI registration
        _ = transformationService;
        _ = processors;
        _logger = logger;
    }

    public Task<TransformationOrchestrationResult> ApplyTransformationsAsync(
        ReadBatch batch,
        Guid pipelineId,
        TransformationPolicy policy,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        
        // NOTE: Transformations are now embedded in field mappings
        // This orchestrator returns the batch unchanged - transformations are applied in field mapping phase
        _logger.LogDebug(
            "TransformationOrchestrator called for pipeline {PipelineId} - transformations are handled in field mappings",
            pipelineId);

        stopwatch.Stop();
        
        var result = new TransformationOrchestrationResult
        {
            BatchId = batch.BatchId,
            InitialRowCount = batch.RowCount,
            FinalRowCount = batch.RowCount,
            TransformedBatch = batch,
            Success = true,
            TotalExecutionTime = stopwatch.Elapsed
        };

        return Task.FromResult(result);
    }
}
