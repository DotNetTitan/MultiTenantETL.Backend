using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MultiTenantETL.Application.Common.Interfaces;
using MultiTenantETL.Application.Interfaces;
using MultiTenantETL.Domain.Entities;
using MultiTenantETL.Infrastructure.Persistence;
using System.Text.Json;

namespace MultiTenantETL.Infrastructure.Services;

public class AuditService : IAuditService
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<AuditService> _logger;

    public AuditService(
        ApplicationDbContext context,
        ICurrentUserService currentUser,
        IHttpContextAccessor httpContextAccessor,
        ILogger<AuditService> logger)
    {
        _context = context;
        _currentUser = currentUser;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public async Task LogAsync(
        string action,
        string resourceType,
        string? resourceId = null,
        string? description = null,
        object? metadata = null,
        string severity = "Info",
        bool success = true,
        string? errorMessage = null,
        Guid? tenantIdOverride = null)
    {
        try
        {
            var httpContext = _httpContextAccessor.HttpContext;

            // Auto-set severity to Error if success is false and no specific severity was provided
            if (!success && severity == "Info")
            {
                severity = "Error";
            }

            // Resolve tenant ID safely to avoid FK violations when context has no active tenant
            // (CurrentUserService returns Guid.Empty when tenant cannot be determined).
            Guid? tenantId = null;
            var currentTenantId = tenantIdOverride ?? _currentUser.GetTenantId();
            if (currentTenantId != Guid.Empty)
            {
                var tenantExists = await _context.Tenants
                    .IgnoreQueryFilters()
                    .AsNoTracking()
                    .AnyAsync(t => t.Id == currentTenantId);

                if (tenantExists)
                {
                    tenantId = currentTenantId;
                }
                else
                {
                    _logger.LogWarning(
                        "Skipping tenant association for audit action {Action}: tenant {TenantId} was not found.",
                        action,
                        currentTenantId);
                }
            }

            var auditLog = new AuditLog
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                UserId = _currentUser.GetUserId(),
                UserEmail = _currentUser.GetEmail(),
                Action = action,
                ResourceType = resourceType,
                ResourceId = resourceId,
                Description = description ?? action,
                IpAddress = httpContext?.Connection.RemoteIpAddress?.ToString(),
                UserAgent = httpContext?.Request.Headers["User-Agent"].ToString(),
                Metadata = metadata != null ? JsonSerializer.Serialize(metadata) : null,
                Severity = severity,
                Success = success,
                ErrorMessage = errorMessage,
                CreatedAt = DateTime.UtcNow
            };

            _context.AuditLogs.Add(auditLog);
            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            // Never let audit logging break the application
            _logger.LogError(ex, "Failed to write audit log for action {Action}", action);
        }
    }

    public async Task LogAuthenticationAsync(
        string action,
        string? userEmail = null,
        bool success = true,
        string? errorMessage = null)
    {
        try
        {
            var httpContext = _httpContextAccessor.HttpContext;

            var auditLog = new AuditLog
            {
                Id = Guid.NewGuid(),
                TenantId = null, // Auth events are system-level
                UserId = null,
                UserEmail = userEmail,
                Action = action,
                ResourceType = "Authentication",
                Description = $"{action} - {userEmail}",
                IpAddress = httpContext?.Connection.RemoteIpAddress?.ToString(),
                UserAgent = httpContext?.Request.Headers["User-Agent"].ToString(),
                Severity = success ? "Info" : "Error",
                Success = success,
                ErrorMessage = errorMessage,
                CreatedAt = DateTime.UtcNow
            };

            _context.AuditLogs.Add(auditLog);
            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write authentication audit log for action {Action}", action);
        }
    }

    public async Task<(List<AuditLogDto> Logs, int TotalCount)> GetAuditLogsAsync(
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
        var query = _context.AuditLogs
            .IgnoreQueryFilters()
            .Include(a => a.Tenant)
            .AsQueryable();

        // Apply filters
        if (tenantId.HasValue)
            query = query.Where(a => a.TenantId == tenantId.Value);

        if (userId.HasValue)
            query = query.Where(a => a.UserId == userId.Value);

        if (!string.IsNullOrEmpty(action))
            query = query.Where(a => a.Action == action);

        if (!string.IsNullOrEmpty(resourceType))
            query = query.Where(a => a.ResourceType == resourceType);

        if (!string.IsNullOrEmpty(severity))
        {
            var severityLower = severity.ToLower();
            if (severityLower == "error")
            {
                // When filtering by Error, include both logs with Error severity AND logs where Success is false
                query = query.Where(a => a.Severity.ToLower() == "error" || a.Success == false);
            }
            else
            {
                query = query.Where(a => a.Severity.ToLower() == severityLower);
            }
        }

        if (startDate.HasValue)
            query = query.Where(a => a.CreatedAt >= startDate.Value);

        if (endDate.HasValue)
            query = query.Where(a => a.CreatedAt <= endDate.Value);

        var totalCount = await query.CountAsync();

        var logs = await query
            .OrderByDescending(a => a.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new AuditLogDto
            {
                Id = a.Id,
                TenantId = a.TenantId,
                TenantName = a.Tenant != null ? a.Tenant.Name : null,
                UserId = a.UserId,
                UserEmail = a.UserEmail,
                Action = a.Action,
                ResourceType = a.ResourceType,
                ResourceId = a.ResourceId,
                Description = a.Description,
                IpAddress = a.IpAddress,
                Metadata = a.Metadata,
                Severity = a.Severity,
                Success = a.Success,
                ErrorMessage = a.ErrorMessage,
                CreatedAt = a.CreatedAt
            })
            .ToListAsync();

        return (logs, totalCount);
    }

    public async Task<AuditLogDto?> GetAuditLogByIdAsync(Guid id, Guid? tenantId = null)
    {
        var query = _context.AuditLogs
            .IgnoreQueryFilters()
            .Include(a => a.Tenant)
            .Where(a => a.Id == id);

        if (tenantId.HasValue)
        {
            query = query.Where(a => a.TenantId == tenantId.Value);
        }

        return await query
            .Select(a => new AuditLogDto
            {
                Id = a.Id,
                TenantId = a.TenantId,
                TenantName = a.Tenant != null ? a.Tenant.Name : null,
                UserId = a.UserId,
                UserEmail = a.UserEmail,
                Action = a.Action,
                ResourceType = a.ResourceType,
                ResourceId = a.ResourceId,
                Description = a.Description,
                IpAddress = a.IpAddress,
                Metadata = a.Metadata,
                Severity = a.Severity,
                Success = a.Success,
                ErrorMessage = a.ErrorMessage,
                CreatedAt = a.CreatedAt
            })
            .FirstOrDefaultAsync();
    }
}
