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
        string? errorMessage = null);
    
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
}

/// <summary>
/// DTO for audit log responses
/// </summary>
public class AuditLogDto
{
    public Guid Id { get; set; }
    public Guid? TenantId { get; set; }
    public string? TenantName { get; set; }
    public Guid? UserId { get; set; }
    public string? UserEmail { get; set; }
    public string Action { get; set; } = string.Empty;
    public string ResourceType { get; set; } = string.Empty;
    public string? ResourceId { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? IpAddress { get; set; }
    public string? Metadata { get; set; }
    public string Severity { get; set; } = "Info";
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; }
}
