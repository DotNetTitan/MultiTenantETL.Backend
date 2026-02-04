using Microsoft.AspNetCore.SignalR;
using MultiTenantETL.API.Hubs;
using MultiTenantETL.Application.Executions.Models;
using MultiTenantETL.Application.Executions.Notifications;

namespace MultiTenantETL.API.Services;

public class SignalRExecutionNotificationService : IExecutionNotificationService
{
    private readonly IHubContext<ExecutionHub, IExecutionHubClient> _hubContext;
    private readonly ILogger<SignalRExecutionNotificationService> _logger;

    public SignalRExecutionNotificationService(
        IHubContext<ExecutionHub, IExecutionHubClient> hubContext,
        ILogger<SignalRExecutionNotificationService> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task NotifyExecutionStatusChangedAsync(
        Guid tenantId, 
        ExecutionStatusUpdate update, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            var groupName = $"tenant_{tenantId}";
            await _hubContext.Clients.Group(groupName)
                .ExecutionStatusChanged(update);
            
            _logger.LogDebug(
                "Notified execution status change: ExecutionId={ExecutionId}, Status={Status}, TenantId={TenantId}",
                update.ExecutionId, update.Status, tenantId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "Failed to send execution status notification: ExecutionId={ExecutionId}, TenantId={TenantId}",
                update.ExecutionId, tenantId);
        }
    }

    public async Task NotifyExecutionProgressAsync(
        Guid tenantId, 
        ExecutionProgressUpdate update, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            var groupName = $"tenant_{tenantId}";
            await _hubContext.Clients.Group(groupName)
                .ExecutionProgressUpdated(update);
            
            _logger.LogDebug(
                "Notified execution progress: ExecutionId={ExecutionId}, RecordsProcessed={RecordsProcessed}, TenantId={TenantId}",
                update.ExecutionId, update.RecordsProcessed, tenantId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "Failed to send execution progress notification: ExecutionId={ExecutionId}, TenantId={TenantId}",
                update.ExecutionId, tenantId);
        }
    }

    public async Task NotifyExecutionLogAddedAsync(
        Guid tenantId, 
        Guid executionId, 
        ExecutionLogDto logEntry, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            var groupName = $"tenant_{tenantId}";
            await _hubContext.Clients.Group(groupName)
                .ExecutionLogAdded(new { ExecutionId = executionId, Log = logEntry });
            
            _logger.LogDebug(
                "Notified execution log added: ExecutionId={ExecutionId}, TenantId={TenantId}",
                executionId, tenantId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "Failed to send execution log notification: ExecutionId={ExecutionId}, TenantId={TenantId}",
                executionId, tenantId);
        }
    }
}
