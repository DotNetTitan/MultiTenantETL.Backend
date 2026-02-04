# Real-Time Communication (SignalR) Specification

**For:** ASP.NET Core Web API + SignalR  
**Version:** 1.0  
**Last Updated:** 2025-11-19

---

## Overview

SignalR provides real-time bidirectional communication for pipeline execution progress updates, logs, and status changes.

---

## SignalR Configuration

### Package Installation

```bash
dotnet add package Microsoft.AspNetCore.SignalR
```

### Startup Configuration

```csharp
// Startup.cs or Program.cs
public void ConfigureServices(IServiceCollection services)
{
    services.AddSignalR(options =>
    {
        options.EnableDetailedErrors = env.IsDevelopment();
        options.KeepAliveInterval = TimeSpan.FromSeconds(15);
        options.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
        options.HandshakeTimeout = TimeSpan.FromSeconds(15);
        options.MaximumReceiveMessageSize = 128 * 1024; // 128 KB
    });
}

public void Configure(IApplicationBuilder app)
{
    app.UseEndpoints(endpoints =>
    {
        endpoints.MapHub<ExecutionHub>("/hubs/executions");
    });
}
```

---

## Execution Hub

### Hub Implementation

```csharp
[Authorize]
public class ExecutionHub : Hub
{
    private readonly IExecutionService _executionService;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<ExecutionHub> _logger;

    public ExecutionHub(
        IExecutionService executionService,
        ICurrentUserService currentUser,
        ILogger<ExecutionHub> logger)
    {
        _executionService = executionService;
        _currentUser = currentUser;
        _logger = logger;
    }

    /// <summary>
    /// Join a group to receive updates for a specific execution
    /// </summary>
    public async Task JoinExecution(Guid executionId)
    {
        // Verify user has access to this execution
        var execution = await _executionService.GetExecutionAsync(executionId);
        if (execution == null || execution.TenantId != _currentUser.GetTenantId())
        {
            throw new HubException("Access denied");
        }

        var groupName = GetExecutionGroupName(executionId);
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
        
        _logger.LogInformation(
            "User {UserId} joined execution {ExecutionId}",
            _currentUser.GetUserId(),
            executionId
        );
    }

    /// <summary>
    /// Leave an execution group
    /// </summary>
    public async Task LeaveExecution(Guid executionId)
    {
        var groupName = GetExecutionGroupName(executionId);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
        
        _logger.LogInformation(
            "User {UserId} left execution {ExecutionId}",
            _currentUser.GetUserId(),
            executionId
        );
    }

    /// <summary>
    /// Get current execution status
    /// </summary>
    public async Task<ExecutionStatus> GetExecutionStatus(Guid executionId)
    {
        var execution = await _executionService.GetExecutionAsync(executionId);
        if (execution == null || execution.TenantId != _currentUser.GetTenantId())
        {
            throw new HubException("Execution not found");
        }

        return new ExecutionStatus
        {
            ExecutionId = execution.Id,
            Status = execution.StatusCode,
            ProgressPercent = execution.ProgressPercent,
            RecordsProcessed = execution.RecordsProcessed,
            StartTime = execution.StartTime,
            EndTime = execution.EndTime
        };
    }

    public override async Task OnConnectedAsync()
    {
        var userId = _currentUser.GetUserId();
        var tenantId = _currentUser.GetTenantId();
        
        _logger.LogInformation(
            "SignalR connection established: {ConnectionId}, User: {UserId}, Tenant: {TenantId}",
            Context.ConnectionId,
            userId,
            tenantId
        );

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception exception)
    {
        _logger.LogInformation(
            "SignalR connection closed: {ConnectionId}, Exception: {Exception}",
            Context.ConnectionId,
            exception?.Message
        );

        await base.OnDisconnectedAsync(exception);
    }

    private static string GetExecutionGroupName(Guid executionId) 
        => $"execution_{executionId}";
}
```

---

## Server-to-Client Events

### Progress Update

```csharp
public class ProgressUpdate
{
    public Guid ExecutionId { get; set; }
    public int ProgressPercent { get; set; }
    public long RecordsProcessed { get; set; }
    public string CurrentStep { get; set; }
    public DateTime Timestamp { get; set; }
}

// Send from backend
await _hubContext.Clients
    .Group($"execution_{executionId}")
    .SendAsync("ProgressUpdate", new ProgressUpdate
    {
        ExecutionId = executionId,
        ProgressPercent = 45,
        RecordsProcessed = 5000,
        CurrentStep = "Transforming data",
        Timestamp = DateTime.UtcNow
    });
```

### Status Change

```csharp
public class StatusChangeEvent
{
    public Guid ExecutionId { get; set; }
    public string OldStatus { get; set; }
    public string NewStatus { get; set; }
    public DateTime Timestamp { get; set; }
}

// Send from backend
await _hubContext.Clients
    .Group($"execution_{executionId}")
    .SendAsync("StatusChanged", new StatusChangeEvent
    {
        ExecutionId = executionId,
        OldStatus = "running",
        NewStatus = "completed",
        Timestamp = DateTime.UtcNow
    });
```

### Log Entry

```csharp
public class LogEntryEvent
{
    public Guid ExecutionId { get; set; }
    public DateTime Timestamp { get; set; }
    public string Level { get; set; } // INFO, WARN, ERROR
    public string Message { get; set; }
}

// Send from backend
await _hubContext.Clients
    .Group($"execution_{executionId}")
    .SendAsync("LogAdded", new LogEntryEvent
    {
        ExecutionId = executionId,
        Timestamp = DateTime.UtcNow,
        Level = "INFO",
        Message = "Processing batch 5/10"
    });
```

### Execution Completed

```csharp
public class ExecutionCompletedEvent
{
    public Guid ExecutionId { get; set; }
    public string Status { get; set; }
    public long RecordsProcessed { get; set; }
    public int DurationMs { get; set; }
    public List<string> Errors { get; set; }
    public DateTime CompletedAt { get; set; }
}

// Send from backend
await _hubContext.Clients
    .Group($"execution_{executionId}")
    .SendAsync("ExecutionCompleted", new ExecutionCompletedEvent
    {
        ExecutionId = executionId,
        Status = "completed",
        RecordsProcessed = 12345,
        DurationMs = 300000,
        Errors = new List<string>(),
        CompletedAt = DateTime.UtcNow
    });
```

---

## Client Integration (Frontend)

### TypeScript/JavaScript Client

```typescript
import * as signalR from "@microsoft/signalr";

class ExecutionHubClient {
  private connection: signalR.HubConnection;

  constructor(accessToken: string) {
    this.connection = new signalR.HubConnectionBuilder()
      .withUrl("http://localhost:5000/hubs/executions", {
        accessTokenFactory: () => accessToken
      })
      .withAutomaticReconnect({
        nextRetryDelayInMilliseconds: (retryContext) => {
          // Exponential backoff: 0s, 2s, 10s, 30s
          if (retryContext.previousRetryCount === 0) return 0;
          if (retryContext.previousRetryCount === 1) return 2000;
          if (retryContext.previousRetryCount === 2) return 10000;
          return 30000;
        }
      })
      .configureLogging(signalR.LogLevel.Information)
      .build();

    this.setupEventHandlers();
  }

  private setupEventHandlers(): void {
    // Handle reconnection
    this.connection.onreconnecting((error) => {
      console.log("SignalR reconnecting...", error);
    });

    this.connection.onreconnected((connectionId) => {
      console.log("SignalR reconnected:", connectionId);
    });

    this.connection.onclose((error) => {
      console.log("SignalR connection closed:", error);
    });
  }

  async start(): Promise<void> {
    try {
      await this.connection.start();
      console.log("SignalR connected");
    } catch (err) {
      console.error("SignalR connection error:", err);
      setTimeout(() => this.start(), 5000);
    }
  }

  async joinExecution(executionId: string): Promise<void> {
    await this.connection.invoke("JoinExecution", executionId);
  }

  async leaveExecution(executionId: string): Promise<void> {
    await this.connection.invoke("LeaveExecution", executionId);
  }

  onProgressUpdate(callback: (update: ProgressUpdate) => void): void {
    this.connection.on("ProgressUpdate", callback);
  }

  onStatusChanged(callback: (event: StatusChangeEvent) => void): void {
    this.connection.on("StatusChanged", callback);
  }

  onLogAdded(callback: (log: LogEntryEvent) => void): void {
    this.connection.on("LogAdded", callback);
  }

  onExecutionCompleted(callback: (result: ExecutionCompletedEvent) => void): void {
    this.connection.on("ExecutionCompleted", callback);
  }

  async stop(): Promise<void> {
    await this.connection.stop();
  }
}

// Usage
const hubClient = new ExecutionHubClient(accessToken);
await hubClient.start();
await hubClient.joinExecution(executionId);

hubClient.onProgressUpdate((update) => {
  console.log(`Progress: ${update.progressPercent}%`);
  console.log(`Records: ${update.recordsProcessed}`);
});

hubClient.onLogAdded((log) => {
  console.log(`[${log.level}] ${log.message}`);
});

hubClient.onExecutionCompleted((result) => {
  console.log(`Execution completed: ${result.status}`);
});
```

---

## Backend Service Integration

### Execution Service

```csharp
public class ExecutionService : IExecutionService
{
    private readonly IHubContext<ExecutionHub> _hubContext;
    private readonly ILogger<ExecutionService> _logger;

    public async Task ExecutePipelineAsync(Guid pipelineId)
    {
        var execution = await CreateExecutionAsync(pipelineId);
        var groupName = $"execution_{execution.Id}";

        try
        {
            // Notify start
            await _hubContext.Clients.Group(groupName).SendAsync("StatusChanged", 
                new StatusChangeEvent
                {
                    ExecutionId = execution.Id,
                    OldStatus = "idle",
                    NewStatus = "running",
                    Timestamp = DateTime.UtcNow
                });

            // Execute pipeline with progress updates
            await foreach (var progress in ExecutePipelineWithProgressAsync(execution))
            {
                // Update progress
                await _hubContext.Clients.Group(groupName).SendAsync("ProgressUpdate", 
                    new ProgressUpdate
                    {
                        ExecutionId = execution.Id,
                        ProgressPercent = progress.Percent,
                        RecordsProcessed = progress.RecordsProcessed,
                        CurrentStep = progress.Step,
                        Timestamp = DateTime.UtcNow
                    });

                // Send logs
                foreach (var log in progress.Logs)
                {
                    await _hubContext.Clients.Group(groupName).SendAsync("LogAdded", 
                        new LogEntryEvent
                        {
                            ExecutionId = execution.Id,
                            Timestamp = log.Timestamp,
                            Level = log.Level,
                            Message = log.Message
                        });
                }
            }

            // Notify completion
            await _hubContext.Clients.Group(groupName).SendAsync("ExecutionCompleted", 
                new ExecutionCompletedEvent
                {
                    ExecutionId = execution.Id,
                    Status = "completed",
                    RecordsProcessed = execution.RecordsProcessed,
                    DurationMs = execution.DurationMs,
                    Errors = new List<string>(),
                    CompletedAt = DateTime.UtcNow
                });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Pipeline execution failed");

            await _hubContext.Clients.Group(groupName).SendAsync("ExecutionCompleted", 
                new ExecutionCompletedEvent
                {
                    ExecutionId = execution.Id,
                    Status = "failed",
                    RecordsProcessed = execution.RecordsProcessed,
                    DurationMs = execution.DurationMs,
                    Errors = new List<string> { ex.Message },
                    CompletedAt = DateTime.UtcNow
                });
        }
    }

    private async IAsyncEnumerable<ExecutionProgress> ExecutePipelineWithProgressAsync(
        Execution execution)
    {
        var totalRecords = await GetTotalRecordsAsync(execution.SourceId);
        var processedRecords = 0L;

        await foreach (var batch in ProcessBatchesAsync(execution))
        {
            processedRecords += batch.Count;
            var percent = (int)((processedRecords / (double)totalRecords) * 100);

            yield return new ExecutionProgress
            {
                Percent = percent,
                RecordsProcessed = processedRecords,
                Step = $"Processing batch {batch.Number}/{batch.Total}",
                Logs = batch.Logs
            };
        }
    }
}
```

---

## Connection Lifecycle Management

### Connection Tracking

```csharp
public class ConnectionMapping<T>
{
    private readonly Dictionary<T, HashSet<string>> _connections = new();

    public int Count => _connections.Count;

    public void Add(T key, string connectionId)
    {
        lock (_connections)
        {
            if (!_connections.TryGetValue(key, out var connections))
            {
                connections = new HashSet<string>();
                _connections.Add(key, connections);
            }

            connections.Add(connectionId);
        }
    }

    public HashSet<string> GetConnections(T key)
    {
        lock (_connections)
        {
            return _connections.TryGetValue(key, out var connections) 
                ? connections 
                : new HashSet<string>();
        }
    }

    public void Remove(T key, string connectionId)
    {
        lock (_connections)
        {
            if (!_connections.TryGetValue(key, out var connections))
            {
                return;
            }

            connections.Remove(connectionId);

            if (connections.Count == 0)
            {
                _connections.Remove(key);
            }
        }
    }
}

// Track user connections
private static readonly ConnectionMapping<Guid> _userConnections = new();

public override async Task OnConnectedAsync()
{
    var userId = _currentUser.GetUserId();
    _userConnections.Add(userId, Context.ConnectionId);
    
    await base.OnConnectedAsync();
}

public override async Task OnDisconnectedAsync(Exception exception)
{
    var userId = _currentUser.GetUserId();
    _userConnections.Remove(userId, Context.ConnectionId);
    
    await base.OnDisconnectedAsync(exception);
}
```

---

## Error Handling

### Hub Exception Handling

```csharp
public class ErrorHandlingHub : Hub
{
    protected override async Task OnDisconnectedAsync(Exception exception)
    {
        if (exception != null)
        {
            _logger.LogError(exception, "Hub disconnected with error");
            
            // Send error to monitoring
            await _monitoring.TrackExceptionAsync(exception);
        }

        await base.OnDisconnectedAsync(exception);
    }
}

// In hub methods
public async Task JoinExecution(Guid executionId)
{
    try
    {
        // Validation and logic
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error joining execution {ExecutionId}", executionId);
        throw new HubException("Unable to join execution. Please try again.");
    }
}
```

### Client Reconnection

```typescript
connection.onreconnecting((error) => {
  // Show reconnecting UI
  showNotification("Connection lost. Reconnecting...", "warning");
});

connection.onreconnected((connectionId) => {
  // Rejoin groups
  const activeExecutions = getActiveExecutions();
  for (const executionId of activeExecutions) {
    await connection.invoke("JoinExecution", executionId);
  }
  
  showNotification("Reconnected successfully", "success");
});

connection.onclose((error) => {
  showNotification("Connection closed. Please refresh the page.", "error");
});
```

---

## Scalability with Redis Backplane

### For Multiple Server Instances

```csharp
// Install package
// dotnet add package Microsoft.AspNetCore.SignalR.StackExchangeRedis

public void ConfigureServices(IServiceCollection services)
{
    services.AddSignalR()
        .AddStackExchangeRedis(options =>
        {
            options.Configuration.EndPoints.Add("localhost:6379");
            options.Configuration.ChannelPrefix = "MultiTenantETL";
        });
}
```

---

## Testing

### Integration Tests

```csharp
[Fact]
public async Task ExecutionHub_SendsProgressUpdates()
{
    // Arrange
    var hubConnection = new HubConnectionBuilder()
        .WithUrl("http://localhost:5000/hubs/executions")
        .Build();

    var progressReceived = false;
    hubConnection.On<ProgressUpdate>("ProgressUpdate", update =>
    {
        progressReceived = true;
        Assert.Equal(50, update.ProgressPercent);
    });

    await hubConnection.StartAsync();
    await hubConnection.InvokeAsync("JoinExecution", executionId);

    // Act
    await _executionService.ExecutePipelineAsync(pipelineId);

    // Assert
    await Task.Delay(1000); // Wait for message
    Assert.True(progressReceived);
}
```

---

## Monitoring

### Track Hub Metrics

```csharp
public class ExecutionHub : Hub
{
    private readonly IMetricsService _metrics;

    public override async Task OnConnectedAsync()
    {
        _metrics.IncrementCounter("signalr.connections.total");
        _metrics.IncrementGauge("signalr.connections.active");
        
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception exception)
    {
        _metrics.DecrementGauge("signalr.connections.active");
        
        if (exception != null)
        {
            _metrics.IncrementCounter("signalr.disconnections.errors");
        }
        
        await base.OnDisconnectedAsync(exception);
    }
}
```
