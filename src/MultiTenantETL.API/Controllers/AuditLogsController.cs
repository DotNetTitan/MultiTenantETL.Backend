using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MultiTenantETL.Application.Common.Interfaces;
using MultiTenantETL.API.Authorization;
using MultiTenantETL.Application.Interfaces;

namespace MultiTenantETL.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AuditLogsController : ControllerBase
{
    private readonly IAuditService _auditService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAdminAuthorizationService _adminAuthorizationService;
    private readonly ILogger<AuditLogsController> _logger;

    public AuditLogsController(
        IAuditService auditService,
        ICurrentUserService currentUserService,
        IAdminAuthorizationService adminAuthorizationService,
        ILogger<AuditLogsController> logger)
    {
        _auditService = auditService;
        _currentUserService = currentUserService;
        _adminAuthorizationService = adminAuthorizationService;
        _logger = logger;
    }

    /// <summary>
    /// Get audit logs with filtering (global admins)
    /// </summary>
    [HttpGet]
    [Authorize]
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
        if (!await _adminAuthorizationService.IsGlobalAdminAsync())
        {
            return Forbid();
        }

        Guid? tenantFilter = null;

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
    [Authorize]
    public async Task<IActionResult> GetAuditLogById(Guid id)
    {
        if (!await _adminAuthorizationService.IsGlobalAdminAsync())
        {
            return Forbid();
        }

        var log = await _auditService.GetAuditLogByIdAsync(id);

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
