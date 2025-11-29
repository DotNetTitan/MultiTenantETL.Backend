using MultiTenantETL.Domain.Enums;
using MultiTenantETL.Domain.Interfaces;

namespace MultiTenantETL.Domain.Entities;

public class PipelineExecution : ITenantResource
{
    public Guid Id { get; set; }
    public Guid PipelineId { get; set; }
    public Guid TenantId { get; set; }
    
    public ExecutionStatus Status { get; set; }
    
    // Timing (using DateTimeOffset for proper UTC handling)
    public DateTimeOffset StartTime { get; set; }
    public DateTimeOffset? EndTime { get; set; }
    public TimeSpan? Duration { get; set; }
    
    // Metrics (using long for large datasets)
    public long RecordsProcessed { get; set; }
    public long RecordsSucceeded { get; set; }
    public long RecordsFailed { get; set; }
    public decimal ProgressPercent { get; set; }
    
    // Batch tracking
    public int BatchCount { get; set; }
    
    // Error handling
    public string? ErrorMessage { get; set; }
    
    // Compact summary (not full logs - those are in execution_logs table)
    public string? SummaryJson { get; set; }
    
    // Additional metadata
    public string? MetadataJson { get; set; }
    
    // Trigger information
    public required string TriggeredBy { get; set; } // Manual, Scheduled, API
    public Guid? TriggeredByUserId { get; set; }
    
    // Audit
    public DateTimeOffset CreatedAt { get; set; }
    
    // Navigation properties
    public Pipeline? Pipeline { get; set; }
    public Tenant? Tenant { get; set; }
}
