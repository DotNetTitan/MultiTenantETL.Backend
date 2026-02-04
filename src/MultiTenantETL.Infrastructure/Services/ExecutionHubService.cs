using Microsoft.Extensions.Logging;
using MultiTenantETL.Application.Executions;
using MultiTenantETL.Application.Executions.Models;

namespace MultiTenantETL.Infrastructure.Services;

/// <summary>
/// Default implementation of ExecutionHubService that does nothing
/// This allows the Worker to function without SignalR
/// The API layer will register a proper implementation with SignalR hub context
/// </summary>
public class ExecutionHubService : IExecutionHubService
{
    private readonly ILogger<ExecutionHubService> _logger;

    public ExecutionHubService(ILogger<ExecutionHubService> logger)
    {
        _logger = logger;
    }

    public virtual Task SendLogAsync(Guid executionId, Guid tenantId, ExecutionLogDto log, CancellationToken cancellationToken = default)
    {
        _logger.LogTrace("SendLogAsync called but no SignalR context available");
        return Task.CompletedTask;
    }

    public virtual Task SendStatusUpdateAsync(Guid executionId, Guid tenantId, ExecutionStatusUpdate update, CancellationToken cancellationToken = default)
    {
        _logger.LogTrace("SendStatusUpdateAsync called but no SignalR context available");
        return Task.CompletedTask;
    }

    public virtual Task SendStatsUpdateAsync(Guid executionId, Guid tenantId, ExecutionProgressUpdate stats, CancellationToken cancellationToken = default)
    {
        _logger.LogTrace("SendStatsUpdateAsync called but no SignalR context available");
        return Task.CompletedTask;
    }

    public virtual Task SendCompletionAsync(Guid executionId, Guid tenantId, ExecutionCompletionUpdate completion, CancellationToken cancellationToken = default)
    {
        _logger.LogTrace("SendCompletionAsync called but no SignalR context available");
        return Task.CompletedTask;
    }
}
