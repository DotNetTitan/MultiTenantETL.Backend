using Microsoft.AspNetCore.SignalR;
using MultiTenantETL.API.Hubs;
using MultiTenantETL.Application.Executions;
using MultiTenantETL.Application.Executions.Models;

namespace MultiTenantETL.API.Services;

/// <summary>
/// SignalR-enabled implementation of ExecutionHubService
/// </summary>
public class SignalRExecutionHubService : IExecutionHubService
{
    private readonly IHubContext<ExecutionHub> _hubContext;
    private readonly ILogger<SignalRExecutionHubService> _logger;

    public SignalRExecutionHubService(
        IHubContext<ExecutionHub> hubContext,
        ILogger<SignalRExecutionHubService> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task SendLogAsync(Guid executionId, Guid tenantId, ExecutionLogDto log, CancellationToken cancellationToken = default)
    {
        try
        {
            // Send to execution-specific group
            await _hubContext.Clients
                .Group($"execution_{executionId}")
                .SendAsync("ReceiveLog", log, cancellationToken);

            _logger.LogTrace("Sent log to execution_{ExecutionId}", executionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending log via SignalR for execution {ExecutionId}", executionId);
        }
    }

    public async Task SendStatusUpdateAsync(Guid executionId, Guid tenantId, ExecutionStatusUpdate update, CancellationToken cancellationToken = default)
    {
        try
        {
            // Send to execution-specific group and tenant group
            await Task.WhenAll(
                _hubContext.Clients
                    .Group($"execution_{executionId}")
                    .SendAsync("ReceiveStatusUpdate", update, cancellationToken),
                _hubContext.Clients
                    .Group($"tenant_{tenantId}")
                    .SendAsync("ExecutionStatusChanged", new { executionId, update.Status, update.Timestamp }, cancellationToken)
            );

            _logger.LogDebug("Sent status update to execution_{ExecutionId}: {Status}", executionId, update.Status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending status update via SignalR for execution {ExecutionId}", executionId);
        }
    }

    public async Task SendStatsUpdateAsync(Guid executionId, Guid tenantId, ExecutionProgressUpdate stats, CancellationToken cancellationToken = default)
    {
        try
        {
            // Send to execution-specific group
            await _hubContext.Clients
                .Group($"execution_{executionId}")
                .SendAsync("ReceiveProgressUpdate", stats, cancellationToken);

            _logger.LogTrace("Sent progress update to execution_{ExecutionId}: {Progress}%", executionId, stats.ProgressPercent);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending stats update via SignalR for execution {ExecutionId}", executionId);
        }
    }

    public async Task SendCompletionAsync(Guid executionId, Guid tenantId, ExecutionCompletionUpdate completion, CancellationToken cancellationToken = default)
    {
        try
        {
            // Send to execution-specific group and tenant group
            await Task.WhenAll(
                _hubContext.Clients
                    .Group($"execution_{executionId}")
                    .SendAsync("ReceiveCompletion", completion, cancellationToken),
                _hubContext.Clients
                    .Group($"tenant_{tenantId}")
                    .SendAsync("ExecutionCompleted", new { executionId, completion.Status, completion.RecordsProcessed }, cancellationToken)
            );

            _logger.LogInformation("Sent completion notification to execution_{ExecutionId}: {Status}", executionId, completion.Status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending completion via SignalR for execution {ExecutionId}", executionId);
        }
    }
}
