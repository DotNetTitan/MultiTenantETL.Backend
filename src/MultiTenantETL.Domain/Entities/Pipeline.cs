using MultiTenantETL.Domain.Interfaces;

namespace MultiTenantETL.Domain.Entities;

/// <summary>
/// Represents an ETL pipeline that moves data from a source to a destination.
/// </summary>
public class Pipeline : ITenantResource
{
    /// <summary>
    /// Unique identifier for the pipeline.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Tenant that owns this pipeline.
    /// </summary>
    public Guid TenantId { get; set; }

    /// <summary>
    /// Name of the pipeline.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Optional description of what the pipeline does.
    /// </summary>
    public string? Description { get; set; }
    
    // Source and Destination connectors
    
    /// <summary>
    /// Connector ID for reading data.
    /// </summary>
    public Guid SourceConnectorId { get; set; }

    /// <summary>
    /// Connector ID for writing data.
    /// </summary>
    public Guid DestinationConnectorId { get; set; }
    
    // Status: Idle, Running, Failed, Disabled
    
    /// <summary>
    /// Current status of the pipeline (Idle, Running, Failed, Disabled).
    /// </summary>
    public required string Status { get; set; }
    
    // JSON columns for flexible configuration (PostgreSQL JSONB)
    
    /// <summary>
    /// JSON array of field mappings with transformations.
    /// </summary>
    public required string FieldMappingsJson { get; set; }
    
    // Pipeline active state (can be used to disable a pipeline without deleting)
    
    /// <summary>
    /// Whether the pipeline is active and can be executed.
    /// </summary>
    public bool IsActive { get; set; }
    
    // Notification settings - email addresses to notify after pipeline execution
    
    /// <summary>
    /// JSON array of email addresses to notify after execution.
    /// </summary>
    public string? NotificationEmailsJson { get; set; }

    // Email notification enabled state (allows disabling emails without removing addresses)
    
    /// <summary>
    /// Whether email notifications are enabled for this pipeline.
    /// </summary>
    public bool EmailNotificationsEnabled { get; set; } = true;

    // Execution tracking
    
    /// <summary>
    /// When the pipeline was last executed.
    /// </summary>
    public DateTime? LastRunAt { get; set; }

    /// <summary>
    /// Status of the last execution (Completed, Failed, Cancelled).
    /// </summary>
    public string? LastRunStatus { get; set; }

    /// <summary>
    /// Number of records processed in the last execution.
    /// </summary>
    public int? LastRunRecordsProcessed { get; set; }
    
    // Audit fields
    
    /// <summary>
    /// When the pipeline was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// When the pipeline was last updated.
    /// </summary>
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// User who created the pipeline.
    /// </summary>
    public Guid CreatedBy { get; set; }

    /// <summary>
    /// User who last updated the pipeline.
    /// </summary>
    public Guid? UpdatedBy { get; set; }

    // Navigation properties
    
    /// <summary>
    /// Tenant that owns this pipeline.
    /// </summary>
    public Tenant? Tenant { get; set; }

    /// <summary>
    /// Source connector for reading data.
    /// </summary>
    public Connector? SourceConnector { get; set; }

    /// <summary>
    /// Destination connector for writing data.
    /// </summary>
    public Connector? DestinationConnector { get; set; }

    /// <summary>
    /// Schedule for this pipeline if configured.
    /// </summary>
    public PipelineSchedule? Schedule { get; set; }
}
