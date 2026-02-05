using Microsoft.Extensions.Logging;
using MultiTenantETL.Application.Executions.Models;
using MultiTenantETL.Application.Executions.Notifications;

namespace MultiTenantETL.Infrastructure.Services;

/// <summary>
/// Null implementation of IExecutionNotificationService for background worker scenarios
/// where SignalR notifications are not available (worker is a console app without HTTP context)
/// </summary>
public class NullExecutionNotificationService : IExecutionNotificationService
{
    private readonly ILogger<NullExecutionNotificationService> _logger;

    public NullExecutionNotificationService(ILogger<NullExecutionNotificationService> logger)
    {
        _logger = logger;
    }

    public Task NotifyExecutionStatusChangedAsync(
        Guid tenantId, 
        ExecutionStatusUpdate update, 
        CancellationToken cancellationToken = default)
    {
        // No-op: Worker doesn't have SignalR hub context
        _logger.LogDebug(
            "Execution status changed: ExecutionId={ExecutionId}, Status={Status} (notification not sent - worker context)",
            update.ExecutionId, update.Status);
        return Task.CompletedTask;
    }

    public Task NotifyExecutionProgressAsync(
        Guid tenantId, 
        ExecutionProgressUpdate update, 
        CancellationToken cancellationToken = default)
    {
        // No-op: Worker doesn't have SignalR hub context
        _logger.LogDebug(
            "Execution progress updated: ExecutionId={ExecutionId}, Progress={Progress}% (notification not sent - worker context)",
            update.ExecutionId, update.ProgressPercent);
        return Task.CompletedTask;
    }

    public Task NotifyExecutionLogAddedAsync(
        Guid tenantId, 
        Guid executionId, 
        ExecutionLogDto logEntry, 
        CancellationToken cancellationToken = default)
    {
        // No-op: Worker doesn't have SignalR hub context
        _logger.LogDebug(
            "Execution log added: ExecutionId={ExecutionId}, Level={Level} (notification not sent - worker context)",
            executionId, logEntry.Level);
        return Task.CompletedTask;
    }
}
