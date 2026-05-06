using MultiTenantETL.Application.Interfaces;

namespace MultiTenantETL.Infrastructure.Services;

/// <summary>
/// Null implementation of IAuditService for background worker scenarios
/// where audit logging is not needed (actions are already audited at API level)
/// </summary>
public class NullAuditService : IAuditService
{
    public Task LogAsync(
        string action,
        string resourceType,
        string? resourceId = null,
        string? description = null,
        object? metadata = null,
        string severity = "Info",
        bool success = true,
        string? errorMessage = null)
    {
        // No-op: Worker actions don't need separate audit logs
        // The pipeline execution itself is already tracked
        return Task.CompletedTask;
    }

    public Task LogAuthenticationAsync(
        string action,
        string? userEmail = null,
        bool success = true,
        string? errorMessage = null)
    {
        // No-op: Worker doesn't perform authentication
        return Task.CompletedTask;
    }

    public Task<(List<AuditLogDto> Logs, int TotalCount)> GetAuditLogsAsync(
        Guid? tenantId = null,
        Guid? userId = null,
        string? action = null,
        string? resourceType = null,
        string? severity = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        int page = 1,
        int pageSize = 50)
    {
        // No-op: Worker doesn't query audit logs
        return Task.FromResult((new List<AuditLogDto>(), 0));
    }

    public Task<AuditLogDto?> GetAuditLogByIdAsync(Guid id, Guid? tenantId = null)
    {
        // No-op: Worker doesn't query audit logs
        return Task.FromResult<AuditLogDto?>(null);
    }
}
