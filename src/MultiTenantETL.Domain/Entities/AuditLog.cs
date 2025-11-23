namespace MultiTenantETL.Domain.Entities;

/// <summary>
/// Audit log entry for tracking user actions and system events
/// </summary>
public class AuditLog
{
    public Guid Id { get; set; }
    
    /// <summary>
    /// Tenant context for the action (null for system-level actions)
    /// </summary>
    public Guid? TenantId { get; set; }
    
    /// <summary>
    /// User who performed the action (null for system actions)
    /// </summary>
    public Guid? UserId { get; set; }
    
    /// <summary>
    /// User's email at the time of action
    /// </summary>
    public string? UserEmail { get; set; }
    
    /// <summary>
    /// Action performed (e.g., "User.Login", "Pipeline.Created", "Connector.Deleted")
    /// </summary>
    public string Action { get; set; } = string.Empty;
    
    /// <summary>
    /// Resource type (e.g., "User", "Tenant", "Pipeline", "Connector")
    /// </summary>
    public string ResourceType { get; set; } = string.Empty;
    
    /// <summary>
    /// Resource ID that was affected (if applicable)
    /// </summary>
    public string? ResourceId { get; set; }
    
    /// <summary>
    /// Human-readable description of the action
    /// </summary>
    public string Description { get; set; } = string.Empty;
    
    /// <summary>
    /// IP address of the user
    /// </summary>
    public string? IpAddress { get; set; }
    
    /// <summary>
    /// User agent string
    /// </summary>
    public string? UserAgent { get; set; }
    
    /// <summary>
    /// Additional metadata as JSON (old values, new values, etc.)
    /// </summary>
    public string? Metadata { get; set; }
    
    /// <summary>
    /// Severity level (Info, Warning, Error)
    /// </summary>
    public string Severity { get; set; } = "Info";
    
    /// <summary>
    /// Whether the action was successful
    /// </summary>
    public bool Success { get; set; } = true;
    
    /// <summary>
    /// Error message if action failed
    /// </summary>
    public string? ErrorMessage { get; set; }
    
    /// <summary>
    /// When the action occurred
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    public virtual Tenant? Tenant { get; set; }
}
