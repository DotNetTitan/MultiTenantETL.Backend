using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MultiTenantETL.Application.Common.Interfaces;
using MultiTenantETL.Application.Messaging;
using MultiTenantETL.Domain.Entities;
using MultiTenantETL.Domain.Enums;
using MultiTenantETL.Infrastructure.Persistence;
using Quartz;

namespace MultiTenantETL.Infrastructure.Scheduling;

/// <summary>
/// Quartz job that triggers pipeline execution based on schedule.
/// This job is created with JobData containing the schedule ID and tenant ID.
/// </summary>
[DisallowConcurrentExecution] // Prevent overlapping executions of the same pipeline
public class PipelineScheduleJob : IJob
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<PipelineScheduleJob> _logger;

    // Job data keys
    public const string ScheduleIdKey = "ScheduleId";
    public const string PipelineIdKey = "PipelineId";
    public const string TenantIdKey = "TenantId";

    public PipelineScheduleJob(
        IServiceProvider serviceProvider,
        ILogger<PipelineScheduleJob> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var scheduleId = Guid.Parse(context.MergedJobDataMap.GetString(ScheduleIdKey) ?? string.Empty);
        var pipelineId = Guid.Parse(context.MergedJobDataMap.GetString(PipelineIdKey) ?? string.Empty);
        var tenantId = Guid.Parse(context.MergedJobDataMap.GetString(TenantIdKey) ?? string.Empty);

        _logger.LogInformation(
            "Executing scheduled pipeline job: ScheduleId={ScheduleId}, PipelineId={PipelineId}, TenantId={TenantId}",
            scheduleId, pipelineId, tenantId);

        // Create a new scope to get scoped services
        using var scope = _serviceProvider.CreateScope();
        
        // Set tenant context for this job
        var tenantProvider = scope.ServiceProvider.GetRequiredService<ITenantProvider>();
        tenantProvider.TenantId = tenantId;
        tenantProvider.CorrelationId = $"schedule-{scheduleId}";

        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var messagePublisher = scope.ServiceProvider.GetRequiredService<IMessagePublisher>();

        try
        {
            // Get the schedule and pipeline (use IgnoreQueryFilters to bypass tenant filter since we set context)
            var schedule = await dbContext.PipelineSchedules
                .Include(s => s.Pipeline)
                .FirstOrDefaultAsync(s => s.Id == scheduleId && s.TenantId == tenantId);

            if (schedule == null)
            {
                _logger.LogWarning("Schedule not found: ScheduleId={ScheduleId}", scheduleId);
                return;
            }

            if (!schedule.IsActive)
            {
                _logger.LogInformation("Schedule is not active, skipping: ScheduleId={ScheduleId}", scheduleId);
                return;
            }

            var pipeline = schedule.Pipeline;
            if (pipeline == null)
            {
                _logger.LogWarning("Pipeline not found for schedule: ScheduleId={ScheduleId}", scheduleId);
                return;
            }

            if (!pipeline.IsActive)
            {
                _logger.LogInformation("Pipeline is not active, skipping: PipelineId={PipelineId}", pipelineId);
                return;
            }

            // Create execution record
            var execution = new PipelineExecution
            {
                Id = Guid.NewGuid(),
                PipelineId = pipelineId,
                TenantId = tenantId,
                Status = ExecutionStatus.Queued,
                StartTime = DateTimeOffset.UtcNow,
                RecordsProcessed = 0,
                RecordsSucceeded = 0,
                RecordsFailed = 0,
                ProgressPercent = 0,
                BatchCount = 0,
                TriggeredBy = "Scheduled",
                TriggeredByUserId = null, // System-triggered
                CreatedAt = DateTimeOffset.UtcNow
            };

            dbContext.PipelineExecutions.Add(execution);

            // Update schedule's last run time
            schedule.LastRunAt = DateTimeOffset.UtcNow;
            
            // Calculate next run time
            var cronExpression = new CronExpression(schedule.CronExpression);
            var timeZone = TimeZoneInfo.FindSystemTimeZoneById(schedule.Timezone);
            var nextFireTime = cronExpression.GetNextValidTimeAfter(DateTimeOffset.UtcNow);
            if (nextFireTime.HasValue)
            {
                schedule.NextRunAt = nextFireTime.Value;
            }

            await dbContext.SaveChangesAsync();

            // Create initial log entry
            var logEntry = new ExecutionLogEntry
            {
                Id = Guid.NewGuid(),
                ExecutionId = execution.Id,
                TenantId = tenantId,
                Timestamp = DateTimeOffset.UtcNow,
                Level = "Info",
                Source = "Scheduler",
                Message = $"Pipeline execution triggered by schedule",
                Details = $"Schedule: {schedule.CronExpression}, Pipeline: {pipeline.Name}",
                CreatedAt = DateTimeOffset.UtcNow
            };

            dbContext.ExecutionLogs.Add(logEntry);
            await dbContext.SaveChangesAsync();

            // Publish execution task to RabbitMQ
            var executionTask = new ExecutionTask
            {
                ExecutionId = execution.Id,
                PipelineId = pipelineId,
                TenantId = tenantId,
                BatchSize = 1000,
                DryRun = false,
                QueuedAt = DateTimeOffset.UtcNow
            };

            await messagePublisher.PublishExecutionTaskAsync(executionTask);

            _logger.LogInformation(
                "Successfully queued scheduled execution: ExecutionId={ExecutionId}, PipelineId={PipelineId}",
                execution.Id, pipelineId);

            // Reset consecutive failures on successful queue
            if (schedule.ConsecutiveFailures > 0)
            {
                schedule.ConsecutiveFailures = 0;
                await dbContext.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "Error executing scheduled pipeline: ScheduleId={ScheduleId}, PipelineId={PipelineId}",
                scheduleId, pipelineId);

            // Update consecutive failures
            try
            {
                var schedule = await dbContext.PipelineSchedules
                    .FirstOrDefaultAsync(s => s.Id == scheduleId && s.TenantId == tenantId);

                if (schedule != null)
                {
                    schedule.ConsecutiveFailures++;
                    schedule.LastRunStatus = "Failed";

                    // Disable schedule if max failures reached
                    if (schedule.ConsecutiveFailures >= schedule.MaxConsecutiveFailures)
                    {
                        schedule.IsActive = false;
                        _logger.LogWarning(
                            "Schedule disabled due to {Failures} consecutive failures: ScheduleId={ScheduleId}",
                            schedule.ConsecutiveFailures, scheduleId);
                    }

                    await dbContext.SaveChangesAsync();
                }
            }
            catch (Exception updateEx)
            {
                _logger.LogError(updateEx, "Failed to update schedule failure count: ScheduleId={ScheduleId}", scheduleId);
            }

            throw; // Re-throw to let Quartz handle the error
        }
    }
}
