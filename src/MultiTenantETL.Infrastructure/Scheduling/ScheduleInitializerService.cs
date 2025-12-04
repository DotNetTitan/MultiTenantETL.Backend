using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MultiTenantETL.Infrastructure.Persistence;
using Quartz;

namespace MultiTenantETL.Infrastructure.Scheduling;

/// <summary>
/// Background service that initializes Quartz jobs for active schedules on application startup.
/// This ensures that schedules are restored after application restarts.
/// </summary>
public class ScheduleInitializerService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ISchedulerFactory _schedulerFactory;
    private readonly ILogger<ScheduleInitializerService> _logger;

    public ScheduleInitializerService(
        IServiceProvider serviceProvider,
        ISchedulerFactory schedulerFactory,
        ILogger<ScheduleInitializerService> logger)
    {
        _serviceProvider = serviceProvider;
        _schedulerFactory = schedulerFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Wait a bit for the application to fully start
        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

        try
        {
            _logger.LogInformation("Initializing pipeline schedules from database...");

            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var scheduler = await _schedulerFactory.GetScheduler(stoppingToken);

            // Get all active schedules across all tenants (no tenant filter for initialization)
            var activeSchedules = await dbContext.PipelineSchedules
                .IgnoreQueryFilters() // Need to load schedules for all tenants
                .Include(s => s.Pipeline)
                .Where(s => s.IsActive && s.Pipeline != null && s.Pipeline.IsActive)
                .ToListAsync(stoppingToken);

            _logger.LogInformation("Found {Count} active schedules to initialize", activeSchedules.Count);

            foreach (var schedule in activeSchedules)
            {
                try
                {
                    var jobKey = new JobKey(schedule.QuartzJobKey ?? $"pipeline-{schedule.PipelineId}-{schedule.Id}");
                    var triggerKey = new TriggerKey(schedule.QuartzTriggerKey ?? $"trigger-{schedule.PipelineId}-{schedule.Id}");

                    // Skip if job already exists
                    if (await scheduler.CheckExists(jobKey, stoppingToken))
                    {
                        _logger.LogDebug("Schedule {ScheduleId} already registered in Quartz", schedule.Id);
                        continue;
                    }

                    // Create job detail
                    var jobDetail = JobBuilder.Create<PipelineScheduleJob>()
                        .WithIdentity(jobKey)
                        .WithDescription($"Scheduled execution for pipeline: {schedule.Pipeline?.Name}")
                        .UsingJobData(PipelineScheduleJob.ScheduleIdKey, schedule.Id.ToString())
                        .UsingJobData(PipelineScheduleJob.PipelineIdKey, schedule.PipelineId.ToString())
                        .UsingJobData(PipelineScheduleJob.TenantIdKey, schedule.TenantId.ToString())
                        .StoreDurably(true)
                        .Build();

                    // Create trigger with cron expression
                    TimeZoneInfo timeZone;
                    try
                    {
                        timeZone = TimeZoneInfo.FindSystemTimeZoneById(schedule.Timezone);
                    }
                    catch (TimeZoneNotFoundException)
                    {
                        timeZone = TimeZoneInfo.Utc;
                        _logger.LogWarning(
                            "Invalid timezone '{Timezone}' for schedule {ScheduleId}, using UTC",
                            schedule.Timezone, schedule.Id);
                    }

                    var trigger = TriggerBuilder.Create()
                        .WithIdentity(triggerKey)
                        .ForJob(jobKey)
                        .WithCronSchedule(schedule.CronExpression, x => x.InTimeZone(timeZone))
                        .WithDescription($"Cron trigger: {schedule.CronExpression} ({schedule.Timezone})")
                        .Build();

                    await scheduler.ScheduleJob(jobDetail, trigger, stoppingToken);

                    // Update next run time in database
                    var nextFireTime = trigger.GetNextFireTimeUtc();
                    if (nextFireTime.HasValue && schedule.NextRunAt != nextFireTime.Value)
                    {
                        schedule.NextRunAt = nextFireTime.Value;
                    }

                    _logger.LogInformation(
                        "Registered schedule {ScheduleId} for pipeline '{PipelineName}', next fire: {NextFire}",
                        schedule.Id, schedule.Pipeline?.Name, nextFireTime);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to initialize schedule {ScheduleId}", schedule.Id);
                }
            }

            // Save any updated next run times
            await dbContext.SaveChangesAsync(stoppingToken);

            _logger.LogInformation("Pipeline schedule initialization completed");
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Schedule initialization cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing pipeline schedules");
        }
    }
}
