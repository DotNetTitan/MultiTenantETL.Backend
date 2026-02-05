# Real-Time Notification Architecture

## Overview

This document explains how real-time execution notifications work in the multi-tenant ETL system, particularly how the **Worker** (which executes pipelines) sends notifications to the **Frontend** (via the API's SignalR hub).

## The Challenge

Initially, there was a fundamental architecture issue:

1. **Worker executes pipelines** - It runs `PipelineOrchestrator` which generates logs, progress updates, and status changes
2. **API hosts SignalR** - The web application has the SignalR hub for WebSocket connections to frontend clients
3. **Worker is a console app** - It cannot host SignalR hub (no HTTP/WebSocket infrastructure)

The question was: **How does the Worker send real-time notifications if it can't host SignalR?**

## Solution: HTTP Relay Pattern

```
┌─────────────────────────────────────────────────────────────────┐
│                         EXECUTION FLOW                           │
└─────────────────────────────────────────────────────────────────┘

1. API receives request to start pipeline
   └─> Publishes ExecutionTask to message queue (RabbitMQ/Service Bus)

2. Worker consumes ExecutionTask from queue
   └─> Creates PipelineOrchestrator
       └─> Executes pipeline (reads, transforms, writes data)
           └─> Generates events:
               ├─> Status changes (Running → Completed/Failed)
               ├─> Progress updates (after each batch)
               └─> Log entries (as they occur)

3. PipelineOrchestrator calls IExecutionNotificationService
   └─> Worker has HttpExecutionNotificationService
       └─> POSTs to API internal endpoints

4. API NotificationsController receives HTTP POST
   └─> Calls IExecutionNotificationService (SignalR implementation)
       └─> Broadcasts via SignalR hub

5. Frontend clients receive real-time updates via WebSocket
   └─> UI updates automatically
```

## Architecture Components

### Worker Side

**HttpExecutionNotificationService**
- Implementation of `IExecutionNotificationService` for Worker
- Uses HttpClient to POST notifications to API
- Configured with API base URL from appsettings.json
- Error handling: logs failures, doesn't break pipeline execution

```csharp
// Worker/Program.cs
builder.Services.AddHttpClient<IExecutionNotificationService, 
    HttpExecutionNotificationService>(client => {
    client.BaseAddress = new Uri("http://localhost:5244");
    client.Timeout = TimeSpan.FromSeconds(10);
});
```

### API Side

**NotificationsController**
- Internal endpoints for Worker to POST notifications
- Routes:
  - `POST /api/internal/notifications/execution-status`
  - `POST /api/internal/notifications/execution-progress`
  - `POST /api/internal/notifications/execution-log`
- Receives from Worker, relays to SignalR

**SignalRExecutionNotificationService**
- Implementation of `IExecutionNotificationService` for API
- Uses `IHubContext<ExecutionHub>` to broadcast via SignalR
- Sends to tenant-specific groups for multi-tenant isolation

```csharp
// API/Program.cs
builder.Services.AddScoped<IExecutionNotificationService,
    SignalRExecutionNotificationService>();
```

## Message Flow Example

### Status Change Notification

```
Worker PipelineOrchestrator:
  execution.Status = ExecutionStatus.Completed
  ↓
  _notificationService.NotifyExecutionStatusChangedAsync(
    tenantId: tenant-123,
    update: { executionId, status: "Completed", endTime, duration }
  )
  ↓
HttpExecutionNotificationService (Worker):
  POST http://api:5244/api/internal/notifications/execution-status
  Body: { tenantId, update }
  ↓
NotificationsController (API):
  [HttpPost("execution-status")]
  NotifyExecutionStatus(notification)
  ↓
  _notificationService.NotifyExecutionStatusChangedAsync(...)
  ↓
SignalRExecutionNotificationService (API):
  _hubContext.Clients.Group("tenant_123")
    .SendAsync("ExecutionStatusChanged", update)
  ↓
SignalR Hub → WebSocket → Frontend Client:
  connection.on("ExecutionStatusChanged", (update) => {
    // Update UI with new status
  })
```

## Configuration

### Worker (appsettings.json)

```json
{
  "ApiBaseUrl": "http://localhost:5244",
  // Can be overridden with environment variable:
  // ApiBaseUrl=http://api-service:80
}
```

### API (Program.cs)

```csharp
// SignalR hub
builder.Services.AddSignalR();
app.MapHub<ExecutionHub>("/hubs/executions");

// Notification service
builder.Services.AddScoped<IExecutionNotificationService,
    SignalRExecutionNotificationService>();
```

## Error Handling

### Worker Side
- HTTP POST failures are logged but don't break pipeline execution
- Pipeline continues even if notifications fail
- Ensures data processing reliability over notification delivery

### API Side
- Controller returns 500 on errors (logged)
- SignalR broadcast failures are logged
- Individual client disconnections don't affect others (group broadcast)

## Security Considerations

### Current Implementation
- Worker POSTs to internal API endpoints
- Endpoints require `[Authorize]` attribute
- TODO: Implement service-to-service authentication

### Recommended Enhancements
1. **API Key Authentication**: Worker includes API key in headers
2. **JWT Tokens**: Worker uses service account JWT
3. **Network Isolation**: API only accepts requests from Worker network
4. **Rate Limiting**: Prevent abuse of notification endpoints

## Scalability

### Multiple Workers
- Multiple Worker instances can run simultaneously
- All POST to the same API
- API broadcasts to all connected clients
- Message queue handles work distribution

### Multiple API Instances
- If API is load-balanced, Worker must:
  - POST to load balancer URL
  - Or use sticky sessions
  - Or implement distributed SignalR backplane (Redis, Azure SignalR Service)

## Performance Considerations

### Notification Frequency
- Status changes: ~4 per execution (Queued, Running, Completed/Failed)
- Progress updates: 1 per batch (typically 10-100 per execution)
- Log entries: 5-20 per batch (50-2000 per execution)

### Optimization Strategies
1. **Batch Log Entries**: Send multiple logs in one HTTP request
2. **Throttle Progress**: Only send every N batches or every X seconds
3. **Async Fire-and-Forget**: Don't await HTTP responses
4. **Connection Pooling**: Reuse HTTP connections (already done by HttpClient)

## Troubleshooting

### Notifications Not Appearing in Frontend

1. **Check Worker Logs**: Look for HTTP POST errors
   ```
   Failed to send execution status notification: StatusCode=500
   ```

2. **Check API Logs**: Verify NotificationsController receives requests
   ```
   Relayed execution status notification: ExecutionId=...
   ```

3. **Check SignalR Connection**: Frontend should show "Live Updates" chip
   ```javascript
   console.log("SignalR connection state:", connection.state)
   ```

4. **Check Tenant Isolation**: Verify frontend user's tenant matches execution tenant

### Worker Can't Connect to API

1. **Check ApiBaseUrl**: Verify configuration points to correct API
2. **Check Network**: Ensure Worker can reach API (firewall, DNS)
3. **Check API is Running**: API must be running before Worker
4. **Check Authentication**: Worker must have valid credentials

## Testing

### Manual Test
1. Start API: `cd src/MultiTenantETL.API && dotnet run`
2. Start Worker: `cd src/MultiTenantETL.Worker && dotnet run`
3. Open Frontend: http://localhost:5173
4. Start a pipeline execution
5. Watch for real-time updates in UI
6. Check logs in both API and Worker

### Verification Points
- ✅ Worker logs show "Sent execution status notification"
- ✅ API logs show "Relayed execution status notification"
- ✅ Frontend shows green "Live Updates" indicator
- ✅ Execution status changes automatically in UI
- ✅ Progress percentage increases in real-time
- ✅ Logs stream into details view

## Future Enhancements

1. **WebSocket from Worker**: Worker could connect to SignalR as a client
2. **Message Queue Pattern**: Publish notifications to queue, API consumes and broadcasts
3. **gRPC**: Use gRPC streaming instead of HTTP REST
4. **Circuit Breaker**: Implement circuit breaker pattern for Worker→API calls
5. **Retry with Backoff**: Retry failed notifications with exponential backoff
