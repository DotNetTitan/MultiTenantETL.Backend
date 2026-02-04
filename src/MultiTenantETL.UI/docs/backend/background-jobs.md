# Background Job Processing with Hangfire

**For:** ASP.NET Core Web API + Hangfire  
**Version:** 1.0  
**Last Updated:** 2025-11-19

---

## Overview

Hangfire manages background job processing for pipeline executions, scheduled tasks, and maintenance operations.

---

## Why Hangfire?

✅ **Persistent storage** - Jobs survive app restarts  
✅ **Automatic retries** - Built-in retry logic  
✅ **Dashboard** - Web UI for monitoring  
✅ **PostgreSQL support** - Native integration  
✅ **Distributed** - Scales across multiple servers  

---

## Installation

```bash
dotnet add package Hangfire.AspNetCore
dotnet add package Hangfire.PostgreSql
```

---

## Configuration

```csharp
// Startup.cs
public void ConfigureServices(IServiceCollection services)
{
    // Add Hangfire with PostgreSQL
    services.AddHangfire(config => config
        .SetDataCompatibilityLevel(CompatibilityLevel.Version_170)
        .UseSimpleAssemblyNameTypeSerializer()
        .UseRecommendedSerializerSettings()
        .UsePostgreSqlStorage(Configuration.GetConnectionString("HangfireConnection"),
            new PostgreSqlStorageOptions
            {
                QueuePollInterval = TimeSpan.FromSeconds(15),
                JobExpirationCheckInterval = TimeSpan.FromHours(1),
                CountersAggregateInterval = TimeSpan.FromMinutes(5),
                PrepareSchemaIfNecessary = true,
                DashboardJobListLimit = 50000,
                TransactionTimeout = TimeSpan.FromMinutes(1),
                SchemaName = "hangfire"
            }));

    // Add Hangfire server
    services.AddHangfireServer(options =>
    {
        options.WorkerCount = Environment.ProcessorCount * 2;
        options.Queues = new[] { "critical", "default", "background" };
        options.ServerName = $"{Environment.MachineName}-{Guid.NewGuid().ToString()[..8]}";
    });
}

public void Configure(IApplicationBuilder app)
{
    // Hangfire Dashboard (secure it!)
    app.UseHangfireDashboard("/hangfire", new DashboardOptions
    {
        Authorization = new[] { new HangfireAuthorizationFilter() },
        StatsPollingInterval = 5000
    });
}
```

---

## Job Types

### 1. Pipeline Execution Job

```csharp
public class ExecutePipelineJob
{
    private readonly IPipelineExecutionEngine _executionEngine;
    private readonly ILogger<ExecutePipelineJob> _logger;

    [AutomaticRetry(Attempts = 3, OnAttemptsExceeded = AttemptsExceededAction.Fail)]
    [DisableConcurrentExecution(timeoutInSeconds: 3600)]
    public async Task ExecuteAsync(Guid pipelineId, Guid executionId, Guid tenantId)
    {
        _logger.LogInformation(
            "Starting pipeline execution: Pipeline={PipelineId}, Execution={ExecutionId}",
            pipelineId, executionId
        );

        try
        {
            await _executionEngine.ExecutePipelineAsync(pipelineId, executionId, tenantId);
            
            _logger.LogInformation(
                "Pipeline execution completed: Execution={ExecutionId}",
                executionId
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Pipeline execution failed: Execution={ExecutionId}",
                executionId
            );
            throw; // Re-throw for Hangfire retry
        }
    }
}
```

### 2. Scheduled Pipeline Job

```csharp
public class ScheduledPipelineJob
{
    private readonly IBackgroundJobClient _backgroundJobs;
    private readonly IPipelineRepository _pipelineRepo;

    [DisableConcurrentExecution(timeoutInSeconds: 300)]
    public async Task CheckAndEnqueueScheduledPipelines()
    {
        var dueSchedules = await _pipelineRepo.GetDueScheduledPipelinesAsync();

        foreach (var pipeline in dueSchedules)
        {
            var executionId = Guid.NewGuid();

            _backgroundJobs.Enqueue<ExecutePipelineJob>(job =>
                job.ExecuteAsync(pipeline.Id, executionId, pipeline.TenantId)
            );

            await _pipelineRepo.UpdateLastRunTimeAsync(pipeline.Id, DateTime.UtcNow);
        }
    }
}

// Register recurring job
RecurringJob.AddOrUpdate<ScheduledPipelineJob>(
    "check-scheduled-pipelines",
    job => job.CheckAndEnqueueScheduledPipelines(),
    Cron.MinuteInterval(1)
);
```

### 3. Schema Detection Job

```csharp
public class SchemaDetectionJob
{
    [AutomaticRetry(Attempts = 2)]
    public async Task DetectSchemaAsync(Guid connectorId)
    {
        var connector = await _connectorRepo.GetByIdAsync(connectorId);
        var schema = await _schemaDetector.DetectAsync(connector);
        
        await _connectorRepo.UpdateSchemaAsync(connectorId, schema);
    }
}
```

### 4. Data Cleanup Job

```csharp
public class DataCleanupJob
{
    [DisableConcurrentExecution(timeoutInSeconds: 7200)]
    public async Task CleanupOldExecutionsAsync()
    {
        var cutoffDate = DateTime.UtcNow.AddDays(-30);
        
        var deletedCount = await _executionRepo.DeleteOlderThanAsync(cutoffDate);
        
        _logger.LogInformation(
            "Cleaned up {Count} old executions",
            deletedCount
        );
    }
}

// Register as recurring job (runs daily at 2 AM)
RecurringJob.AddOrUpdate<DataCleanupJob>(
    "cleanup-old-executions",
    job => job.CleanupOldExecutionsAsync(),
    Cron.Daily(2)
);
```

---

## Enqueuing Jobs

### Fire and Forget

```csharp
// Execute immediately
var jobId = BackgroundJob.Enqueue<ExecutePipelineJob>(job =>
    job.ExecuteAsync(pipelineId, executionId, tenantId)
);
```

### Delayed Execution

```csharp
// Execute after 5 minutes
var jobId = BackgroundJob.Schedule<ExecutePipelineJob>(
    job => job.ExecuteAsync(pipelineId, executionId, tenantId),
    TimeSpan.FromMinutes(5)
);
```

### Recurring Jobs

```csharp
// Execute daily at 2 AM
RecurringJob.AddOrUpdate<DataCleanupJob>(
    "cleanup-job",
    job => job.CleanupOldExecutionsAsync(),
    Cron.Daily(2),
    new RecurringJobOptions
    {
        TimeZone = TimeZoneInfo.FindSystemTimeZoneById("UTC")
    }
);
```

### Continuation Jobs

```csharp
// Execute after parent job completes
var parentJobId = BackgroundJob.Enqueue(() => ProcessData());
BackgroundJob.ContinueJobWith(parentJobId, () => SendNotification());
```

---

## Job Configuration

### Retry Policy

```csharp
[AutomaticRetry(Attempts = 3, DelaysInSeconds = new[] { 60, 300, 900 })]
public async Task ExecuteWithCustomRetries()
{
    // Job logic
}
```

### Timeout

```csharp
[JobTimeout(3600)] // 1 hour timeout
public async Task LongRunningJob()
{
    // Job logic
}
```

### Concurrency Control

```csharp
[DisableConcurrentExecution(timeoutInSeconds: 900)]
public async Task SingletonJob()
{
    // Only one instance runs at a time
}
```

### Queue Priority

```csharp
[Queue("critical")]
public async Task HighPriorityJob()
{
    // Runs in critical queue
}

[Queue("background")]
public async Task LowPriorityJob()
{
    // Runs in background queue
}
```

---

## Dashboard Security

```csharp
public class HangfireAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var httpContext = context.GetHttpContext();
        
        // Only allow authenticated admin users
        return httpContext.User.Identity.IsAuthenticated &&
               httpContext.User.IsInRole("admin");
    }
}
```

---

## Monitoring & Alerts

### Job Filters

```csharp
public class JobExecutionMetricsFilter : IElectStateFilter
{
    private readonly IMetricsService _metrics;

    public void OnStateElection(ElectStateContext context)
    {
        if (context.CandidateState is SucceededState)
        {
            _metrics.IncrementCounter("hangfire.jobs.succeeded");
        }
        else if (context.CandidateState is FailedState)
        {
            _metrics.IncrementCounter("hangfire.jobs.failed");
        }
    }
}

// Register filter
GlobalJobFilters.Filters.Add(new JobExecutionMetricsFilter());
```

### Failed Job Notifications

```csharp
public class FailedJobNotificationFilter : IApplyStateFilter
{
    private readonly INotificationService _notifications;

    public void OnStateApplied(ApplyStateContext context, IWriteOnlyTransaction transaction)
    {
        if (context.NewState is FailedState failedState)
        {
            _notifications.SendAsync(new JobFailedNotification
            {
                JobId = context.BackgroundJob.Id,
                JobType = context.BackgroundJob.Job.Type.Name,
                Exception = failedState.Exception.Message,
                FailedAt = DateTime.UtcNow
            });
        }
    }

    public void OnStateUnapplied(ApplyStateContext context, IWriteOnlyTransaction transaction)
    {
        // Not needed
    }
}
```

---

## Performance Tuning

### Worker Configuration

```csharp
services.AddHangfireServer(options =>
{
    // Number of concurrent jobs
    options.WorkerCount = Environment.ProcessorCount * 2;
    
    // Queue priorities
    options.Queues = new[] { "critical", "default", "background" };
    
    // Polling interval
    options.SchedulePollingInterval = TimeSpan.FromSeconds(15);
    
    // Server check interval
    options.ServerCheckInterval = TimeSpan.FromMinutes(1);
    
    // Heartbeat interval
    options.HeartbeatInterval = TimeSpan.FromSeconds(30);
});
```

### Database Cleanup

```csharp
// Configure expiration times
new PostgreSqlStorageOptions
{
    JobExpirationCheckInterval = TimeSpan.FromHours(1),
    CountersAggregateInterval = TimeSpan.FromMinutes(5)
}

// Create cleanup job
RecurringJob.AddOrUpdate(
    "hangfire-cleanup",
    () => CleanupHangfireData(),
    Cron.Weekly(DayOfWeek.Sunday, 3)
);
```

---

## Testing

### Unit Tests

```csharp
[Fact]
public async Task ExecutePipelineJob_Success()
{
    // Arrange
    var job = new ExecutePipelineJob(_executionEngine, _logger);
    
    // Act
    await job.ExecuteAsync(pipelineId, executionId, tenantId);
    
    // Assert
    _executionEngine.Verify(
        x => x.ExecutePipelineAsync(pipelineId, executionId, tenantId),
        Times.Once
    );
}
```

### Integration Tests

```csharp
[Fact]
public async Task ScheduledPipelineJob_EnqueuesJobs()
{
    // Arrange
    await CreateScheduledPipeline();
    var job = new ScheduledPipelineJob(_backgroundJobs, _pipelineRepo);
    
    // Act
    await job.CheckAndEnqueueScheduledPipelines();
    
    // Assert
    var enqueuedJobs = GetEnqueuedJobs();
    Assert.Single(enqueuedJobs);
}
```
