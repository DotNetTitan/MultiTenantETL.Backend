using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MultiTenantETL.Application.Executions.Models;
using MultiTenantETL.Application.Executions.Notifications;

namespace MultiTenantETL.API.Controllers;

/// <summary>
/// Internal API for Worker to send execution notifications that are broadcast via SignalR
/// </summary>
[ApiController]
[Route("api/internal/notifications")]
[Authorize] // Worker will use service-to-service token
public class NotificationsController : ControllerBase
{
    private readonly IExecutionNotificationService _notificationService;
    private readonly ILogger<NotificationsController> _logger;

    public NotificationsController(
        IExecutionNotificationService notificationService,
        ILogger<NotificationsController> logger)
    {
        _notificationService = notificationService;
        _logger = logger;
    }

    /// <summary>
    /// Worker calls this to broadcast execution status changes via SignalR
    /// </summary>
    [HttpPost("execution-status")]
    public async Task<IActionResult> NotifyExecutionStatus([FromBody] ExecutionStatusNotification notification)
    {
        try
        {
            await _notificationService.NotifyExecutionStatusChangedAsync(
                notification.TenantId,
                notification.Update,
                HttpContext.RequestAborted);

            _logger.LogDebug(
                "Relayed execution status notification: ExecutionId={ExecutionId}, Status={Status}",
                notification.Update.ExecutionId, notification.Update.Status);

            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to relay execution status notification");
            return StatusCode(500, "Failed to process notification");
        }
    }

    /// <summary>
    /// Worker calls this to broadcast execution progress via SignalR
    /// </summary>
    [HttpPost("execution-progress")]
    public async Task<IActionResult> NotifyExecutionProgress([FromBody] ExecutionProgressNotification notification)
    {
        try
        {
            await _notificationService.NotifyExecutionProgressAsync(
                notification.TenantId,
                notification.Update,
                HttpContext.RequestAborted);

            _logger.LogDebug(
                "Relayed execution progress notification: ExecutionId={ExecutionId}, Progress={Progress}%",
                notification.Update.ExecutionId, notification.Update.ProgressPercent);

            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to relay execution progress notification");
            return StatusCode(500, "Failed to process notification");
        }
    }

    /// <summary>
    /// Worker calls this to broadcast execution logs via SignalR
    /// </summary>
    [HttpPost("execution-log")]
    public async Task<IActionResult> NotifyExecutionLog([FromBody] ExecutionLogNotification notification)
    {
        try
        {
            await _notificationService.NotifyExecutionLogAddedAsync(
                notification.TenantId,
                notification.ExecutionId,
                notification.LogEntry,
                HttpContext.RequestAborted);

            _logger.LogDebug(
                "Relayed execution log notification: ExecutionId={ExecutionId}, Level={Level}",
                notification.ExecutionId, notification.LogEntry.Level);

            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to relay execution log notification");
            return StatusCode(500, "Failed to process notification");
        }
    }
}

/// <summary>
/// Wrapper for status notification from Worker
/// </summary>
public class ExecutionStatusNotification
{
    public Guid TenantId { get; set; }
    public ExecutionStatusUpdate Update { get; set; } = null!;
}

/// <summary>
/// Wrapper for progress notification from Worker
/// </summary>
public class ExecutionProgressNotification
{
    public Guid TenantId { get; set; }
    public ExecutionProgressUpdate Update { get; set; } = null!;
}

/// <summary>
/// Wrapper for log notification from Worker
/// </summary>
public class ExecutionLogNotification
{
    public Guid TenantId { get; set; }
    public Guid ExecutionId { get; set; }
    public ExecutionLogDto LogEntry { get; set; } = null!;
}
