using MultiTenantETL.Domain.Interfaces;

namespace MultiTenantETL.Domain.Entities;

public class PipelineExecution : ITenantResource
{
    public Guid Id { get; set; }
    public Guid PipelineId { get; set; }
    public Guid TenantId { get; set; }
    
    // Status: Queued, Running, Completed, Failed, Cancelled
    public required string Status { get; set; }
    
    // Timing
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public long? DurationMs { get; set; } // Duration in milliseconds
    
    // Metrics
    public int RecordsProcessed { get; set; }
    public int RecordsSucceeded { get; set; }
    public int RecordsFailed { get; set; }
    public decimal ProgressPercent { get; set; }
    
    // Error handling
    public string? ErrorMessage { get; set; }
    
    // Logs stored as JSON array
    public required string LogsJson { get; set; }
    
    // Additional metadata
    public string? MetadataJson { get; set; }
    
    // Trigger information
    public required string TriggeredBy { get; set; } // Manual, Scheduled, API
    public Guid? TriggeredByUserId { get; set; }
    
    // Audit
    public DateTime CreatedAt { get; set; }
    
    // Navigation properties
    public Pipeline? Pipeline { get; set; }
    public Tenant? Tenant { get; set; }
}
