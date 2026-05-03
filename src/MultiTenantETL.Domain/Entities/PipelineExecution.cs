using MultiTenantETL.Domain.Enums;
using MultiTenantETL.Domain.Interfaces;

namespace MultiTenantETL.Domain.Entities;

/// <summary>
/// Represents an execution instance of a pipeline.
/// </summary>
public class PipelineExecution : ITenantResource
{
    /// <summary>
    /// Unique identifier for this execution.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Pipeline that was executed.
    /// </summary>
    public Guid PipelineId { get; set; }

    /// <summary>
    /// Tenant that owns this execution.
    /// </summary>
    public Guid TenantId { get; set; }
    
    /// <summary>
    /// Current execution status.
    /// </summary>
    public ExecutionStatus Status { get; set; }
    
    // Timing (using DateTimeOffset for proper UTC handling)
    
    /// <summary>
    /// When the execution started.
    /// </summary>
    public DateTimeOffset StartTime { get; set; }

    /// <summary>
    /// When the execution ended.
    /// </summary>
    public DateTimeOffset? EndTime { get; set; }

    /// <summary>
    /// Total duration of the execution.
    /// </summary>
    public TimeSpan? Duration { get; set; }
    
    // Metrics (using long for large datasets)
    
    /// <summary>
    /// Number of records read from the source.
    /// </summary>
    public long RecordsProcessed { get; set; }

    /// <summary>
    /// Number of records successfully written.
    /// </summary>
    public long RecordsSucceeded { get; set; }

    /// <summary>
    /// Number of records that failed to write.
    /// </summary>
    public long RecordsFailed { get; set; }

    /// <summary>
    /// Overall progress as a percentage (0-100).
    /// </summary>
    public decimal ProgressPercent { get; set; }
    
    // Batch tracking
    
    /// <summary>
    /// Number of batches processed.
    /// </summary>
    public int BatchCount { get; set; }
    
    // Error handling
    
    /// <summary>
    /// Error message if the execution failed.
    /// </summary>
    public string? ErrorMessage { get; set; }
    
    // Compact summary (not full logs - those are in execution_logs table)
    
    /// <summary>
    /// JSON summary of the execution.
    /// </summary>
    public string? SummaryJson { get; set; }
    
    // Additional metadata
    
    /// <summary>
    /// Additional metadata as JSON.
    /// </summary>
    public string? MetadataJson { get; set; }
    
    // Trigger information
    
    /// <summary>
    /// What triggered this execution (Manual, Scheduled, API).
    /// </summary>
    public required string TriggeredBy { get; set; }

    /// <summary>
    /// User ID of who triggered the execution (for manual triggers).
    /// </summary>
    public Guid? TriggeredByUserId { get; set; }
    
    // Audit
    
    /// <summary>
    /// When this execution was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }
    
    // Navigation properties
    
    /// <summary>
    /// The pipeline that was executed.
    /// </summary>
    public Pipeline? Pipeline { get; set; }

    /// <summary>
    /// The tenant that owns this execution.
    /// </summary>
    public Tenant? Tenant { get; set; }
}
