using MultiTenantETL.Domain.Interfaces;

namespace MultiTenantETL.Domain.Entities;

/// <summary>
/// Persistent execution log entry stored in execution_logs table.
/// Separate from PipelineExecution for scalability and partitioning.
/// </summary>
public class ExecutionLogEntry : ITenantResource
{
    /// <summary>
    /// Unique identifier for this log entry.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Execution this entry belongs to.
    /// </summary>
    public Guid ExecutionId { get; set; }

    /// <summary>
    /// Tenant that owns this entry.
    /// </summary>
    public Guid TenantId { get; set; }
    
    /// <summary>
    /// When this entry was created.
    /// </summary>
    public DateTimeOffset Timestamp { get; set; }

    /// <summary>
    /// Log level (Info, Warning, Error, Debug).
    /// </summary>
    public required string Level { get; set; }

    /// <summary>
    /// Source component that created this entry.
    /// </summary>
    public required string Source { get; set; }

    /// <summary>
    /// Log message.
    /// </summary>
    public required string Message { get; set; }

    /// <summary>
    /// Additional details as JSON or formatted text.
    /// </summary>
    public string? Details { get; set; }
    
    // Optional batch tracking
    
    /// <summary>
    /// Batch this entry is associated with (if applicable).
    /// </summary>
    public Guid? BatchId { get; set; }
    
    /// <summary>
    /// When this entry was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }
    
    // Navigation properties
    
    /// <summary>
    /// The execution this entry belongs to.
    /// </summary>
    public PipelineExecution? Execution { get; set; }

    /// <summary>
    /// The tenant that owns this entry.
    /// </summary>
    public Tenant? Tenant { get; set; }
}
