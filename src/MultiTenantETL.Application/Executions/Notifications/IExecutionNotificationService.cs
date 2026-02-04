using MultiTenantETL.Application.Executions.Models;

namespace MultiTenantETL.Application.Executions.Notifications;

public interface IExecutionNotificationService
{
    /// <summary>
    /// Notify clients about execution status change
    /// </summary>
    Task NotifyExecutionStatusChangedAsync(Guid tenantId, ExecutionStatusUpdate update, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Notify clients about execution progress update
    /// </summary>
    Task NotifyExecutionProgressAsync(Guid tenantId, ExecutionProgressUpdate update, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Notify clients about new log entry
    /// </summary>
    Task NotifyExecutionLogAddedAsync(Guid tenantId, Guid executionId, ExecutionLogDto logEntry, CancellationToken cancellationToken = default);
}
