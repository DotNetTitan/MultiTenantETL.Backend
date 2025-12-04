using MultiTenantETL.Domain.Interfaces;

namespace MultiTenantETL.Domain.Entities;

public class Pipeline : ITenantResource
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    
    // Source and Destination connectors
    public Guid SourceConnectorId { get; set; }
    public Guid DestinationConnectorId { get; set; }
    
    // Status: Idle, Running, Failed, Disabled
    public required string Status { get; set; }
    
    // JSON columns for flexible configuration (PostgreSQL JSONB)
    public required string FieldMappingsJson { get; set; } // Array of field mappings with transformations
    
    // Scheduling (managed via PipelineSchedule entity and /api/schedules endpoints)
    public bool IsScheduled { get; set; }
    public bool IsActive { get; set; }
    
    // Execution tracking
    public DateTime? LastRunAt { get; set; }
    public string? LastRunStatus { get; set; } // Completed, Failed, Cancelled
    public int? LastRunRecordsProcessed { get; set; }
    
    // Audit fields
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid CreatedBy { get; set; }
    public Guid? UpdatedBy { get; set; }

    // Navigation properties
    public Tenant? Tenant { get; set; }
    public Connector? SourceConnector { get; set; }
    public Connector? DestinationConnector { get; set; }
}
