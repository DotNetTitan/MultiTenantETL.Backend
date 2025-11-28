using MultiTenantETL.Domain.Interfaces;

namespace MultiTenantETL.Domain.Entities;

/// <summary>
/// Persistent execution log entry stored in execution_logs table
/// Separate from PipelineExecution for scalability and partitioning
/// </summary>
public class ExecutionLogEntry : ITenantResource
{
    public Guid Id { get; set; }
    public Guid ExecutionId { get; set; }
    public Guid TenantId { get; set; }
    
    public DateTimeOffset Timestamp { get; set; }
    public required string Level { get; set; } // Info, Warning, Error, Debug
    public required string Source { get; set; }
    public required string Message { get; set; }
    public string? Details { get; set; }
    
    // Optional batch tracking
    public Guid? BatchId { get; set; }
    
    public DateTimeOffset CreatedAt { get; set; }
    
    // Navigation properties
    public PipelineExecution? Execution { get; set; }
    public Tenant? Tenant { get; set; }
}
