using MultiTenantETL.Domain.Enums;
using MultiTenantETL.Domain.Interfaces;

namespace MultiTenantETL.Domain.Entities;

/// <summary>
/// Tracks individual batch processing within a pipeline execution.
/// Enables fine-grained checkpointing and resumability.
/// </summary>
public class ExecutionBatch : ITenantResource
{
    /// <summary>
    /// Unique identifier for this batch.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Execution this batch belongs to.
    /// </summary>
    public Guid ExecutionId { get; set; }

    /// <summary>
    /// Tenant that owns this batch.
    /// </summary>
    public Guid TenantId { get; set; }
    
    /// <summary>
    /// Index of this batch within the execution.
    /// </summary>
    public int BatchIndex { get; set; }

    /// <summary>
    /// Number of rows in this batch.
    /// </summary>
    public int RowsCount { get; set; }

    /// <summary>
    /// Number of rows that succeeded.
    /// </summary>
    public int RowsSucceeded { get; set; }

    /// <summary>
    /// Number of rows that failed.
    /// </summary>
    public int RowsFailed { get; set; }
    
    /// <summary>
    /// Current status of this batch.
    /// </summary>
    public BatchStatus Status { get; set; }
    
    /// <summary>
    /// When the batch started processing.
    /// </summary>
    public DateTimeOffset? StartedAt { get; set; }

    /// <summary>
    /// When the batch finished.
    /// </summary>
    public DateTimeOffset? EndedAt { get; set; }
    
    // Checkpoint information for resumability (JSONB)
    
    /// <summary>
    /// JSON checkpoint information for resumability.
    /// </summary>
    public string? CheckpointInfoJson { get; set; }
    
    /// <summary>
    /// When this batch was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }
    
    // Navigation properties
    
    /// <summary>
    /// The execution this batch belongs to.
    /// </summary>
    public PipelineExecution? Execution { get; set; }

    /// <summary>
    /// The tenant that owns this batch.
    /// </summary>
    public Tenant? Tenant { get; set; }
}
