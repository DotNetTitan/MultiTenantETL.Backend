using MultiTenantETL.Application.Connectors.DataReaders;
using MultiTenantETL.Domain.Entities;

namespace MultiTenantETL.Application.Transformations;

/// <summary>
/// Interface for transformation processors that operate on batches of data
/// </summary>
public interface ITransformationProcessor
{
    /// <summary>
    /// Process a batch of data through the transformation
    /// </summary>
    /// <param name="batch">The batch to transform</param>
    /// <param name="transformation">The transformation configuration</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Transformation result with transformed rows and metrics</returns>
    Task<TransformationResult> ProcessBatchAsync(
        ReadBatch batch, 
        Transformation transformation, 
        CancellationToken cancellationToken);
    
    /// <summary>
    /// Gets the transformation type this processor handles
    /// </summary>
    string TransformationType { get; }
}

/// <summary>
/// Result of a transformation operation
/// </summary>
public class TransformationResult
{
    public Guid BatchId { get; set; }
    
    /// <summary>
    /// Transformed rows (may be fewer than input if filtered)
    /// </summary>
    public List<Dictionary<string, object?>> TransformedRows { get; set; } = new();
    
    /// <summary>
    /// Number of rows processed
    /// </summary>
    public int RowsProcessed { get; set; }
    
    /// <summary>
    /// Number of rows filtered out
    /// </summary>
    public int RowsFiltered { get; set; }
    
    /// <summary>
    /// Number of rows that had errors
    /// </summary>
    public int RowsWithErrors { get; set; }
    
    /// <summary>
    /// Warnings generated during transformation
    /// </summary>
    public List<TransformationWarning> Warnings { get; set; } = new();
    
    /// <summary>
    /// Errors that occurred during transformation
    /// </summary>
    public List<TransformationError> Errors { get; set; } = new();
    
    /// <summary>
    /// Execution time
    /// </summary>
    public TimeSpan ExecutionTime { get; set; }
}

/// <summary>
/// Warning generated during transformation
/// </summary>
public class TransformationWarning
{
    public int RowIndex { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? FieldName { get; set; }
}

/// <summary>
/// Error that occurred during transformation
/// </summary>
public class TransformationError
{
    public int RowIndex { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? FieldName { get; set; }
    public string? ErrorCode { get; set; }
    public Dictionary<string, object?>? RowData { get; set; }
}
