using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace MultiTenantETL.API.Hubs;

/// <summary>
/// SignalR hub for real-time pipeline execution updates
/// </summary>
[Authorize]
public class ExecutionHub : Hub
{
    private readonly ILogger<ExecutionHub> _logger;

    public ExecutionHub(ILogger<ExecutionHub> logger)
    {
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        var tenantId = Context.User?.FindFirst("tenant_id")?.Value;
        if (!string.IsNullOrEmpty(tenantId))
        {
            // Join tenant group for tenant-specific broadcasts
            await Groups.AddToGroupAsync(Context.ConnectionId, $"tenant_{tenantId}");
            _logger.LogInformation("Client {ConnectionId} connected and joined tenant_{TenantId}", 
                Context.ConnectionId, tenantId);
        }
        
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var tenantId = Context.User?.FindFirst("tenant_id")?.Value;
        if (!string.IsNullOrEmpty(tenantId))
        {
            _logger.LogInformation("Client {ConnectionId} disconnected from tenant_{TenantId}", 
                Context.ConnectionId, tenantId);
        }
        
        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Subscribe to updates for a specific execution
    /// </summary>
    public async Task SubscribeToExecution(string executionId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"execution_{executionId}");
        _logger.LogDebug("Client {ConnectionId} subscribed to execution_{ExecutionId}", 
            Context.ConnectionId, executionId);
    }

    /// <summary>
    /// Unsubscribe from updates for a specific execution
    /// </summary>
    public async Task UnsubscribeFromExecution(string executionId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"execution_{executionId}");
        _logger.LogDebug("Client {ConnectionId} unsubscribed from execution_{ExecutionId}", 
            Context.ConnectionId, executionId);
    }
}
