using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MultiTenantETL.Application.Common.Interfaces;
using MultiTenantETL.Application.Interfaces;
using MultiTenantETL.Domain.Constants;

namespace MultiTenantETL.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AuditLogsController : ControllerBase
{
    private readonly IAuditService _auditService;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<AuditLogsController> _logger;

    public AuditLogsController(
        IAuditService auditService,
        ICurrentUserService currentUserService,
        ILogger<AuditLogsController> logger)
    {
        _auditService = auditService;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    /// <summary>
    /// Get audit logs with filtering
    /// SuperAdmin can see all logs, TenantAdmin can see their tenant's logs
    /// </summary>
    [HttpGet]
    [Authorize(Roles = $"{Roles.SuperAdmin},{Roles.TenantAdmin}")]
    public async Task<IActionResult> GetAuditLogs(
        [FromQuery] Guid? userId = null,
        [FromQuery] string? action = null,
        [FromQuery] string? resourceType = null,
        [FromQuery] string? severity = null,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        var userRole = _currentUserService.GetRole();
        Guid? tenantFilter = null;

        // TenantAdmin can only see their tenant's logs
        if (userRole == Roles.TenantAdmin)
        {
            tenantFilter = _currentUserService.GetTenantId();
        }

        var (logs, totalCount) = await _auditService.GetAuditLogsAsync(
            tenantFilter,
            userId,
            action,
            resourceType,
            severity,
            startDate,
            endDate,
            page,
            pageSize);

        return Ok(new
        {
            logs,
            totalCount,
            page,
            pageSize,
            totalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
        });
    }

    /// <summary>
    /// Get audit log by ID
    /// </summary>
    [HttpGet("{id:guid}")]
    [Authorize(Roles = $"{Roles.SuperAdmin},{Roles.TenantAdmin}")]
    public async Task<IActionResult> GetAuditLogById(Guid id)
    {
        var userRole = _currentUserService.GetRole();
        var tenantFilter = userRole == Roles.TenantAdmin ? _currentUserService.GetTenantId() : (Guid?)null;

        var (logs, _) = await _auditService.GetAuditLogsAsync(
            tenantFilter,
            page: 1,
            pageSize: 1);

        var log = logs.FirstOrDefault(l => l.Id == id);

        if (log == null)
        {
            return NotFound(new { message = "Audit log not found" });
        }

        return Ok(log);
    }

    /// <summary>
    /// Get current user's audit logs
    /// </summary>
    [HttpGet("my-logs")]
    public async Task<IActionResult> GetMyAuditLogs(
        [FromQuery] string? action = null,
        [FromQuery] string? severity = null,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        var userId = _currentUserService.GetUserId();

        var (logs, totalCount) = await _auditService.GetAuditLogsAsync(
            userId: userId,
            action: action,
            severity: severity,
            startDate: startDate,
            endDate: endDate,
            page: page,
            pageSize: pageSize);

        return Ok(new
        {
            logs,
            totalCount,
            page,
            pageSize,
            totalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
        });
    }
}
