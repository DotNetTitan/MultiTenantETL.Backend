using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using MultiTenantETL.API.Hubs;
using MultiTenantETL.Application.Executions;
using MultiTenantETL.Application.Executions.Models;

namespace MultiTenantETL.API.Controllers;

/// <summary>
/// Internal controller for SignalR broadcasting from Worker
/// This allows the Worker to trigger SignalR updates without having direct hub access
/// </summary>
[ApiController]
[Route("api/internal/signalr")]
public class SignalRBroadcastController : ControllerBase
{
    private readonly IHubContext<ExecutionHub> _hubContext;
    private readonly ILogger<SignalRBroadcastController> _logger;

    public SignalRBroadcastController(
        IHubContext<ExecutionHub> hubContext,
        ILogger<SignalRBroadcastController> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    /// <summary>
    /// Broadcast log entry to execution subscribers
    /// </summary>
    [HttpPost("log")]
    public async Task<IActionResult> BroadcastLog([FromBody] BroadcastLogRequest request)
    {
        try
        {
            await _hubContext.Clients
                .Group($"execution_{request.ExecutionId}")
                .SendAsync("ReceiveLog", request.Log);

            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error broadcasting log for execution {ExecutionId}", request.ExecutionId);
            return StatusCode(500, "Error broadcasting log");
        }
    }

    /// <summary>
    /// Broadcast status update to execution and tenant groups
    /// </summary>
    [HttpPost("status")]
    public async Task<IActionResult> BroadcastStatus([FromBody] BroadcastStatusRequest request)
    {
        try
        {
            await Task.WhenAll(
                _hubContext.Clients
                    .Group($"execution_{request.ExecutionId}")
                    .SendAsync("ReceiveStatusUpdate", request.Update),
                _hubContext.Clients
                    .Group($"tenant_{request.TenantId}")
                    .SendAsync("ExecutionStatusChanged", new { request.ExecutionId, request.Update.Status, request.Update.Timestamp })
            );

            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error broadcasting status for execution {ExecutionId}", request.ExecutionId);
            return StatusCode(500, "Error broadcasting status");
        }
    }

    /// <summary>
    /// Broadcast progress update to execution subscribers
    /// </summary>
    [HttpPost("progress")]
    public async Task<IActionResult> BroadcastProgress([FromBody] BroadcastProgressRequest request)
    {
        try
        {
            await _hubContext.Clients
                .Group($"execution_{request.ExecutionId}")
                .SendAsync("ReceiveProgressUpdate", request.Progress);

            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error broadcasting progress for execution {ExecutionId}", request.ExecutionId);
            return StatusCode(500, "Error broadcasting progress");
        }
    }

    /// <summary>
    /// Broadcast completion to execution and tenant groups
    /// </summary>
    [HttpPost("completion")]
    public async Task<IActionResult> BroadcastCompletion([FromBody] BroadcastCompletionRequest request)
    {
        try
        {
            await Task.WhenAll(
                _hubContext.Clients
                    .Group($"execution_{request.ExecutionId}")
                    .SendAsync("ReceiveCompletion", request.Completion),
                _hubContext.Clients
                    .Group($"tenant_{request.TenantId}")
                    .SendAsync("ExecutionCompleted", new { request.ExecutionId, request.Completion.Status, request.Completion.RecordsProcessed })
            );

            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error broadcasting completion for execution {ExecutionId}", request.ExecutionId);
            return StatusCode(500, "Error broadcasting completion");
        }
    }
}

// Request DTOs
public class BroadcastLogRequest
{
    public Guid ExecutionId { get; set; }
    public Guid TenantId { get; set; }
    public required ExecutionLogDto Log { get; set; }
}

public class BroadcastStatusRequest
{
    public Guid ExecutionId { get; set; }
    public Guid TenantId { get; set; }
    public required ExecutionStatusUpdate Update { get; set; }
}

public class BroadcastProgressRequest
{
    public Guid ExecutionId { get; set; }
    public Guid TenantId { get; set; }
    public required ExecutionProgressUpdate Progress { get; set; }
}

public class BroadcastCompletionRequest
{
    public Guid ExecutionId { get; set; }
    public Guid TenantId { get; set; }
    public required ExecutionCompletionUpdate Completion { get; set; }
}
