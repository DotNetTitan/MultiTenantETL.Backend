# Worker Dependency Injection Fix

## Problem

When starting the Worker service, the application crashed with a dependency injection error:

```
System.AggregateException: Some services are not able to be constructed 
(Error while validating the service descriptor 'ServiceType: MultiTenantETL.Application.Orchestration.IPipelineOrchestrator 
Lifetime: Scoped ImplementationType: MultiTenantETL.Infrastructure.Orchestration.PipelineOrchestrator': 
Unable to resolve service for type 'MultiTenantETL.Application.Executions.Notifications.IExecutionNotificationService' 
while attempting to activate 'MultiTenantETL.Infrastructure.Orchestration.PipelineOrchestrator'.)
```

## Root Cause

The issue was introduced when we added SignalR real-time updates functionality:

1. **PipelineOrchestrator** was updated to have `IExecutionNotificationService` as a constructor dependency
2. The **API project** registered `SignalRExecutionNotificationService` as the implementation
3. The **Worker project** also uses `PipelineOrchestrator` but didn't register any implementation
4. When the Worker's DI container tried to create `PipelineOrchestrator`, it couldn't find `IExecutionNotificationService`

## Why Worker Needs Different Implementation

The Worker is a **background console application** that:
- Runs pipeline executions triggered by the API
- Has no HTTP context or SignalR hub
- Cannot send WebSocket notifications to clients
- Doesn't need to notify UI clients (API handles that)

The API is a **web application** that:
- Has HTTP/WebSocket connections
- Hosts the SignalR hub
- Sends real-time updates to connected clients

## Solution

Created a **Null Object Pattern** implementation for the Worker:

### NullExecutionNotificationService

A no-op implementation that:
- Implements `IExecutionNotificationService` interface
- Does nothing when notification methods are called
- Logs debug messages for diagnostics
- Allows Worker to function without SignalR

```csharp
public class NullExecutionNotificationService : IExecutionNotificationService
{
    public Task NotifyExecutionStatusChangedAsync(...) 
    {
        // No-op: Worker doesn't have SignalR hub context
        return Task.CompletedTask;
    }
    // ... other methods similar
}
```

### Registration in Worker

```csharp
// Worker/Program.cs
builder.Services.AddScoped<IExecutionNotificationService,
    NullExecutionNotificationService>();
```

## Benefits

1. **Separation of Concerns**: Worker focuses on processing, API handles communication
2. **No Overhead**: Worker doesn't waste resources trying to send notifications
3. **Clean Architecture**: Both projects can use PipelineOrchestrator with appropriate implementations
4. **Follows Existing Pattern**: Matches the existing `NullAuditService` pattern

## Testing

- ✅ Solution builds successfully
- ✅ Worker starts without DI errors
- ✅ API starts without DI errors  
- ✅ All existing tests pass
- ✅ No regression in functionality

## Files Changed

1. **Created**: `src/MultiTenantETL.Infrastructure/Services/NullExecutionNotificationService.cs`
   - Null implementation for Worker context
   
2. **Updated**: `src/MultiTenantETL.Worker/Program.cs`
   - Registered `NullExecutionNotificationService` in DI container

## Related Files

- API implementation: `src/MultiTenantETL.API/Services/SignalRExecutionNotificationService.cs`
- Interface: `src/MultiTenantETL.Application/Executions/Notifications/IExecutionNotificationService.cs`
- Consumer: `src/MultiTenantETL.Infrastructure/Orchestration/PipelineOrchestrator.cs`
