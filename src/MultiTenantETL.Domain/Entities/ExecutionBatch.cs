using MultiTenantETL.Domain.Interfaces;

namespace MultiTenantETL.Domain.Entities;

/// <summary>
/// Tracks individual batch processing within a pipeline execution
/// Enables fine-grained checkpointing and resumability
/// </summary>
public class ExecutionBatch : ITenantResource
{
    public Guid Id { get; set; }
    public Guid ExecutionId { get; set; }
    public Guid TenantId { get; set; }
    
    public int BatchIndex { get; set; }
    public int RowsCount { get; set; }
    public int RowsSucceeded { get; set; }
    public int RowsFailed { get; set; }
    
    // Status: Queued, Processing, Completed, Failed
    public required string Status { get; set; }
    
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? EndedAt { get; set; }
    
    // Checkpoint information for resumability (JSONB)
    public string? CheckpointInfoJson { get; set; }
    
    public DateTimeOffset CreatedAt { get; set; }
    
    // Navigation properties
    public PipelineExecution? Execution { get; set; }
    public Tenant? Tenant { get; set; }
}
