namespace MultiTenantETL.Application.Interfaces;

/// <summary>
/// Service for logging audit events
/// </summary>
public interface IAuditService
{
    /// <summary>
    /// Log an audit event
    /// </summary>
    Task LogAsync(
        string action,
        string resourceType,
        string? resourceId = null,
        string? description = null,
        object? metadata = null,
        string severity = "Info",
        bool success = true,
        string? errorMessage = null,
        Guid? tenantIdOverride = null);

    /// <summary>
    /// Log an authentication event
    /// </summary>
    Task LogAuthenticationAsync(
        string action,
        string? userEmail = null,
        bool success = true,
        string? errorMessage = null);

    /// <summary>
    /// Get audit logs with filtering
    /// </summary>
    Task<(List<AuditLogDto> Logs, int TotalCount)> GetAuditLogsAsync(
        Guid? tenantId = null,
        Guid? userId = null,
        string? action = null,
        string? resourceType = null,
        string? severity = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        int page = 1,
        int pageSize = 50);

    /// <summary>
    /// Get a single audit log by ID
    /// </summary>
    Task<AuditLogDto?> GetAuditLogByIdAsync(Guid id, Guid? tenantId = null);
}

/// <summary>
/// DTO for audit log responses
/// </summary>
public class AuditLogDto
{
    /// <summary>Unique identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>Tenant ID.</summary>
    public Guid? TenantId { get; set; }

    /// <summary>Tenant name.</summary>
    public string? TenantName { get; set; }

    /// <summary>User ID.</summary>
    public Guid? UserId { get; set; }

    /// <summary>User email.</summary>
    public string? UserEmail { get; set; }

    /// <summary>Action performed.</summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>Type of resource affected.</summary>
    public string ResourceType { get; set; } = string.Empty;

    /// <summary>ID of the resource.</summary>
    public string? ResourceId { get; set; }

    /// <summary>Human-readable description.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>IP address.</summary>
    public string? IpAddress { get; set; }

    /// <summary>Additional metadata as JSON.</summary>
    public string? Metadata { get; set; }

    /// <summary>Severity level.</summary>
    public string Severity { get; set; } = "Info";

    /// <summary>Whether the action succeeded.</summary>
    public bool Success { get; set; }

    /// <summary>Error message if failed.</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>When the event occurred.</summary>
    public DateTime CreatedAt { get; set; }
}
