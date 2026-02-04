using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using MultiTenantETL.Application.Executions;
using MultiTenantETL.Infrastructure.Hubs;

namespace MultiTenantETL.Infrastructure.Services;

/// <summary>
/// Implementation of execution log broadcaster using SignalR
/// </summary>
public class ExecutionLogBroadcaster : IExecutionLogBroadcaster
{
    private readonly IHubContext<PipelineExecutionHub> _hubContext;
    private readonly ILogger<ExecutionLogBroadcaster> _logger;

    public ExecutionLogBroadcaster(
        IHubContext<PipelineExecutionHub> hubContext,
        ILogger<ExecutionLogBroadcaster> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task BroadcastLogEntryAsync(Guid executionId, ExecutionLogBroadcastDto logEntry, CancellationToken cancellationToken = default)
    {
        try
        {
            var groupName = PipelineExecutionHub.GetExecutionGroupName(executionId);
            await _hubContext.Clients.Group(groupName)
                .SendAsync("ReceiveLogEntry", logEntry, cancellationToken);
                
            _logger.LogDebug("Broadcasted log entry for execution {ExecutionId}: {Level} - {Message}", 
                executionId, logEntry.Level, logEntry.Message);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to broadcast log entry for execution {ExecutionId}", executionId);
            // Don't throw - broadcasting failures shouldn't stop execution
        }
    }

    public async Task BroadcastExecutionStatusAsync(Guid executionId, ExecutionStatusBroadcastDto status, CancellationToken cancellationToken = default)
    {
        try
        {
            var groupName = PipelineExecutionHub.GetExecutionGroupName(executionId);
            await _hubContext.Clients.Group(groupName)
                .SendAsync("ReceiveExecutionStatus", status, cancellationToken);
                
            _logger.LogDebug("Broadcasted status update for execution {ExecutionId}: {Status} - {Progress}%", 
                executionId, status.Status, status.ProgressPercent);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to broadcast status for execution {ExecutionId}", executionId);
            // Don't throw - broadcasting failures shouldn't stop execution
        }
    }
}
