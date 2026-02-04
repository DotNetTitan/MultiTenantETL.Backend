# SignalR Real-Time Execution Updates - Complete Implementation

This directory contains the complete SignalR implementation for real-time pipeline execution updates.

## What You Get

✅ **Real-time log streaming** - See logs as they're generated  
✅ **Live progress updates** - Watch records being processed batch by batch  
✅ **Status notifications** - Instant updates when execution status changes  
✅ **Completion alerts** - Know immediately when execution finishes  
✅ **Tenant-isolated** - Each tenant only sees their own executions  
✅ **Vue 3 integration guide** - Complete TypeScript implementation examples  

## Quick Start

### 1. Configuration

**Worker** (`appsettings.json`):
```json
{
  "SignalR": {
    "ApiBaseUrl": "http://localhost:5000",
    "Enabled": true
  }
}
```

**API**: No configuration needed - SignalR is auto-configured

### 2. Connect from Vue 3

```typescript
import { ExecutionHubService } from '@/services/executionHubService';

const hub = new ExecutionHubService(apiUrl, accessToken);
await hub.start();

// Subscribe to execution
await hub.subscribeToExecution(executionId);

// Handle events
hub.onLog((log) => console.log('Log:', log));
hub.onProgressUpdate((progress) => console.log('Progress:', progress));
hub.onCompletion((result) => console.log('Done:', result));
```

### 3. Test It

1. Start API and Worker (via .NET Aspire or manually)
2. Queue a pipeline execution
3. Connect SignalR client
4. Watch real-time updates!

## Documentation

### 📖 Read These First

1. **[SIGNALR_ARCHITECTURE_FIX.md](./docs/SIGNALR_ARCHITECTURE_FIX.md)**  
   **START HERE!** Explains why the original stub implementation was wrong and how the HTTP callback fix works.

2. **[SIGNALR_IMPLEMENTATION.md](./docs/SIGNALR_IMPLEMENTATION.md)**  
   Technical implementation details, architecture diagrams, and component descriptions.

3. **[SIGNALR_VUE3_INTEGRATION.md](./SIGNALR_VUE3_INTEGRATION.md)**  
   Complete Vue 3 + TypeScript integration guide with working code examples.

## Architecture Overview

### The Problem (Original Implementation)

```
❌ Worker used stub → No SignalR updates sent → Users saw nothing!
```

The Worker is where pipelines execute, but it had a no-op SignalR service. Real-time updates never happened.

### The Solution (Current Implementation)

```
✅ Worker → HTTP callback → API → SignalR broadcast → Users see updates!
```

Worker makes HTTP POST requests to API's internal endpoints, which broadcast via SignalR to connected clients.

## Key Components

### API Layer

**ExecutionHub** (`src/MultiTenantETL.API/Hubs/ExecutionHub.cs`)
- SignalR hub at `/hubs/executions`
- JWT authentication required
- Auto-assigns clients to tenant groups

**SignalRBroadcastController** (`src/MultiTenantETL.API/Controllers/SignalRBroadcastController.cs`)
- Internal endpoints for Worker callbacks
- `POST /api/internal/signalr/log`
- `POST /api/internal/signalr/status`
- `POST /api/internal/signalr/progress`
- `POST /api/internal/signalr/completion`

**SignalRExecutionHubService** (`src/MultiTenantETL.API/Services/SignalRExecutionHubService.cs`)
- Direct SignalR implementation for API layer
- Uses `IHubContext<ExecutionHub>`

### Worker Layer

**HttpExecutionHubService** (`src/MultiTenantETL.Infrastructure/Services/HttpExecutionHubService.cs`)
- HTTP-based implementation for Worker
- Calls back to API via HTTP POST
- Fault tolerant (execution continues if broadcast fails)
- Configurable via `SignalR:ApiBaseUrl`

### Shared Layer

**PipelineOrchestrator** (`src/MultiTenantETL.Infrastructure/Orchestration/PipelineOrchestrator.cs`)
- Executes pipelines and sends updates
- Calls `IExecutionHubService` at key points
- Works in both API and Worker contexts

## SignalR Events

### Execution-Specific (Subscribe Required)

- **ReceiveLog** - Log entry
- **ReceiveStatusUpdate** - Status change
- **ReceiveProgressUpdate** - Progress stats
- **ReceiveCompletion** - Final results

### Tenant-Wide (Automatic)

- **ExecutionStatusChanged** - Any execution status change
- **ExecutionCompleted** - Any execution completion

## Configuration Options

### Development (localhost)
```json
{
  "SignalR": {
    "ApiBaseUrl": "http://localhost:5000",
    "Enabled": true
  }
}
```

### .NET Aspire (service discovery)
API URL is automatically resolved from service discovery.

### Docker Compose
```yaml
worker:
  environment:
    - SignalR__ApiBaseUrl=http://api:8080
```

### Production
```json
{
  "SignalR": {
    "ApiBaseUrl": "https://api.yourdomain.com",
    "Enabled": true
  }
}
```

### Disable SignalR (if needed)
```json
{
  "SignalR": {
    "Enabled": false
  }
}
```

## Security

- ✅ JWT authentication required on hub
- ✅ Tenant isolation via automatic grouping
- ✅ Users only receive updates for their tenant
- ✅ Same security as REST API

## Performance Considerations

### Current Implementation (HTTP Callback)
- **Pros**: Simple, no additional infrastructure, works out-of-the-box
- **Cons**: HTTP overhead per update, API must be available
- **Best for**: Development, small/medium deployments

### Future: Redis Backplane (for scale-out)
```csharp
builder.Services.AddSignalR()
    .AddStackExchangeRedis("redis-connection");
```
- **Pros**: No HTTP overhead, both API and Worker can broadcast directly
- **Cons**: Requires Redis infrastructure
- **Best for**: Production, high-scale deployments

## Troubleshooting

### No Updates Received

1. **Check Worker is running** - Pipeline execution happens in Worker
2. **Verify configuration** - `SignalR:ApiBaseUrl` must point to API
3. **Check API is accessible** - Worker must be able to reach API
4. **Enable SignalR logging** - Set `SignalR:Enabled` to true

### Connection Issues

1. **JWT token expired** - Refresh token and reconnect
2. **CORS error** - Verify CORS is configured in API
3. **WebSocket not supported** - SignalR will fallback to SSE/Long polling

### Debug Logging

**Worker:**
```json
{
  "Logging": {
    "LogLevel": {
      "MultiTenantETL.Infrastructure.Services.HttpExecutionHubService": "Debug"
    }
  }
}
```

**API:**
```json
{
  "Logging": {
    "LogLevel": {
      "Microsoft.AspNetCore.SignalR": "Debug"
    }
  }
}
```

## Testing

### Manual Test with Browser Console

```javascript
const connection = new signalR.HubConnectionBuilder()
  .withUrl("http://localhost:5000/hubs/executions", {
    accessTokenFactory: () => "your-jwt-token-here"
  })
  .build();

connection.on("ReceiveLog", (log) => console.log("📝 Log:", log));
connection.on("ReceiveProgressUpdate", (stats) => console.log("📊 Progress:", stats));
connection.on("ReceiveCompletion", (result) => console.log("✅ Done:", result));

await connection.start();
await connection.invoke("SubscribeToExecution", "execution-guid-here");
```

### Integration Test Flow

1. Start API and Worker
2. Create a pipeline via API
3. Queue execution via `POST /api/pipelines/{id}/execute`
4. Connect SignalR client
5. Subscribe to execution
6. Verify updates are received in real-time

## Files Changed

```
src/MultiTenantETL.API/
  ├── Controllers/SignalRBroadcastController.cs    [NEW]
  ├── Hubs/ExecutionHub.cs                         [NEW]
  ├── Services/SignalRExecutionHubService.cs       [NEW]
  └── Program.cs                                   [MODIFIED]

src/MultiTenantETL.Infrastructure/
  ├── Services/
  │   ├── ExecutionHubService.cs                   [DEPRECATED]
  │   └── HttpExecutionHubService.cs               [NEW]
  └── Orchestration/PipelineOrchestrator.cs        [MODIFIED]

src/MultiTenantETL.Worker/
  ├── Program.cs                                   [MODIFIED]
  └── appsettings.json                            [MODIFIED]

src/MultiTenantETL.Application/
  └── Executions/IExecutionHubService.cs          [NEW]

docs/
  ├── SIGNALR_ARCHITECTURE_FIX.md                 [NEW]
  └── SIGNALR_IMPLEMENTATION.md                   [NEW]

SIGNALR_VUE3_INTEGRATION.md                       [NEW]
```

## Credits

Implementation by GitHub Copilot for DotNetTitan/MultiTenantETL

## License

Same as parent project (MultiTenant ETL)
