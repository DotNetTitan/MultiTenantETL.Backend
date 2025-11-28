using MultiTenantETL.Application.Connectors.DataReaders;

namespace MultiTenantETL.Application.Transformations;

/// <summary>
/// Orchestrates the execution of multiple transformations on a batch
/// </summary>
public interface ITransformationOrchestrator
{
    /// <summary>
    /// Apply a sequence of transformations to a batch
    /// </summary>
    /// <param name="batch">The batch to transform</param>
    /// <param name="pipelineId">The pipeline ID for loading transformations</param>
    /// <param name="policy">Policy for handling errors</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Orchestration result with final transformed batch and metrics</returns>
    Task<TransformationOrchestrationResult> ApplyTransformationsAsync(
        ReadBatch batch,
        Guid pipelineId,
        TransformationPolicy policy,
        CancellationToken cancellationToken);
}

/// <summary>
/// Result of applying multiple transformations
/// </summary>
public class TransformationOrchestrationResult
{
    public Guid BatchId { get; set; }
    
    /// <summary>
    /// Final transformed batch
    /// </summary>
    public ReadBatch TransformedBatch { get; set; } = null!;
    
    /// <summary>
    /// Results from each transformation step
    /// </summary>
    public List<TransformationStepResult> StepResults { get; set; } = new();
    
    /// <summary>
    /// Total rows at start
    /// </summary>
    public int InitialRowCount { get; set; }
    
    /// <summary>
    /// Total rows at end
    /// </summary>
    public int FinalRowCount { get; set; }
    
    /// <summary>
    /// Total rows filtered across all steps
    /// </summary>
    public int TotalRowsFiltered { get; set; }
    
    /// <summary>
    /// Total rows with errors across all steps
    /// </summary>
    public int TotalRowsWithErrors { get; set; }
    
    /// <summary>
    /// Total execution time
    /// </summary>
    public TimeSpan TotalExecutionTime { get; set; }
    
    /// <summary>
    /// Whether the orchestration succeeded
    /// </summary>
    public bool Success { get; set; }
    
    /// <summary>
    /// Error message if orchestration failed
    /// </summary>
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Result of a single transformation step
/// </summary>
public class TransformationStepResult
{
    public Guid TransformationId { get; set; }
    public string TransformationType { get; set; } = string.Empty;
    public int Order { get; set; }
    public TransformationResult Result { get; set; } = null!;
}

/// <summary>
/// Policy for handling transformation errors
/// </summary>
public enum TransformationPolicy
{
    /// <summary>
    /// Stop on first error
    /// </summary>
    FailFast,
    
    /// <summary>
    /// Continue processing, skip rows with errors
    /// </summary>
    ContinueOnError,
    
    /// <summary>
    /// Continue processing, keep rows with errors (log warnings)
    /// </summary>
    ContinueWithWarnings
}
