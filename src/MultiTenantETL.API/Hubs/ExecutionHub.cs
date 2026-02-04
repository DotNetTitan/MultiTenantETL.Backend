using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using MultiTenantETL.Application.Common.Interfaces;

namespace MultiTenantETL.API.Hubs;

/// <summary>
/// Client interface defining methods that can be called on connected clients
/// </summary>
public interface IExecutionHubClient
{
    Task ExecutionStatusChanged(object update);
    Task ExecutionProgressUpdated(object update);
    Task ExecutionLogAdded(object logData);
}

[Authorize]
public class ExecutionHub : Hub<IExecutionHubClient>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<ExecutionHub> _logger;

    public ExecutionHub(
        ICurrentUserService currentUserService,
        ILogger<ExecutionHub> logger)
    {
        _currentUserService = currentUserService;
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        var tenantId = _currentUserService.GetTenantId();
        var userId = _currentUserService.GetUserId();
        
        // Add connection to tenant-specific group
        var tenantGroupName = $"tenant_{tenantId}";
        await Groups.AddToGroupAsync(Context.ConnectionId, tenantGroupName);
        
        _logger.LogInformation(
            "Client connected to ExecutionHub: ConnectionId={ConnectionId}, UserId={UserId}, TenantId={TenantId}",
            Context.ConnectionId, userId, tenantId);
        
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var tenantId = _currentUserService.GetTenantId();
        var userId = _currentUserService.GetUserId();
        
        _logger.LogInformation(
            "Client disconnected from ExecutionHub: ConnectionId={ConnectionId}, UserId={UserId}, TenantId={TenantId}",
            Context.ConnectionId, userId, tenantId);
        
        await base.OnDisconnectedAsync(exception);
    }
}
