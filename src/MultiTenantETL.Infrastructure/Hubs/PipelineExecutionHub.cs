using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace MultiTenantETL.Infrastructure.Hubs;

/// <summary>
/// SignalR Hub for real-time pipeline execution log streaming
/// </summary>
[Authorize]
public class PipelineExecutionHub : Hub
{
    private readonly ILogger<PipelineExecutionHub> _logger;

    public PipelineExecutionHub(ILogger<PipelineExecutionHub> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Called when a client subscribes to execution logs for a specific execution
    /// </summary>
    public async Task SubscribeToExecution(Guid executionId)
    {
        var groupName = GetExecutionGroupName(executionId);
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
        _logger.LogInformation("Client {ConnectionId} subscribed to execution {ExecutionId}", 
            Context.ConnectionId, executionId);
    }

    /// <summary>
    /// Called when a client unsubscribes from execution logs
    /// </summary>
    public async Task UnsubscribeFromExecution(Guid executionId)
    {
        var groupName = GetExecutionGroupName(executionId);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
        _logger.LogInformation("Client {ConnectionId} unsubscribed from execution {ExecutionId}", 
            Context.ConnectionId, executionId);
    }

    /// <summary>
    /// Gets the SignalR group name for an execution
    /// </summary>
    public static string GetExecutionGroupName(Guid executionId)
    {
        return $"execution_{executionId}";
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("Client {ConnectionId} connected to PipelineExecutionHub", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (exception != null)
        {
            _logger.LogWarning(exception, "Client {ConnectionId} disconnected with error", Context.ConnectionId);
        }
        else
        {
            _logger.LogInformation("Client {ConnectionId} disconnected", Context.ConnectionId);
        }
        await base.OnDisconnectedAsync(exception);
    }
}
