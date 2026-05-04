using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MultiTenantETL.Application.Common.Interfaces;
using MultiTenantETL.Application.Interfaces;
using MultiTenantETL.Application.Scheduling;
using MultiTenantETL.Application.Scheduling.Models;
using MultiTenantETL.Domain.Constants;
using MultiTenantETL.Domain.Entities;
using MultiTenantETL.Infrastructure.Persistence;
using Quartz;

namespace MultiTenantETL.Infrastructure.Scheduling;

/// <summary>
/// Service for managing pipeline schedules with Quartz.NET integration
/// </summary>
public class ScheduleService : IScheduleService
{
    private readonly ApplicationDbContext _context;
    private readonly ISchedulerFactory _schedulerFactory;
    private readonly ILogger<ScheduleService> _logger;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAuditService _auditService;

    public ScheduleService(
        ApplicationDbContext context,
        ISchedulerFactory schedulerFactory,
        ILogger<ScheduleService> logger,
        ICurrentUserService currentUserService,
        IAuditService auditService)
    {
        _context = context;
        _schedulerFactory = schedulerFactory;
        _logger = logger;
        _currentUserService = currentUserService;
        _auditService = auditService;
    }

    public async Task<ScheduleResponse> CreateAsync(CreateScheduleRequest request, CancellationToken cancellationToken = default)
    {
        var tenantId = _currentUserService.GetTenantId();
        var userId = _currentUserService.GetUserId();

        _logger.LogInformation("Creating schedule for pipeline {PipelineId} in tenant {TenantId}",
            request.PipelineId, tenantId);

        // Verify pipeline exists and belongs to tenant
        var pipeline = await _context.Pipelines
            .FirstOrDefaultAsync(p => p.Id == request.PipelineId && p.TenantId == tenantId, cancellationToken);

        if (pipeline == null)
        {
            throw new KeyNotFoundException($"Pipeline with ID {request.PipelineId} not found");
        }

        // Check if pipeline is active - cannot schedule a deactivated pipeline
        if (!pipeline.IsActive)
        {
            throw new InvalidOperationException($"Cannot create schedule for pipeline {request.PipelineId} because it is deactivated");
        }

        // Check if schedule already exists for this pipeline
        var existingSchedule = await _context.PipelineSchedules
            .FirstOrDefaultAsync(s => s.PipelineId == request.PipelineId, cancellationToken);

        if (existingSchedule != null)
        {
            throw new InvalidOperationException($"A schedule already exists for pipeline {request.PipelineId}. Update or delete the existing schedule.");
        }

        // Validate cron expression
        var validationResult = await ValidateCronExpressionAsync(request.CronExpression, request.Timezone, cancellationToken);
        if (!validationResult.IsValid)
        {
            throw new ArgumentException(validationResult.ErrorMessage);
        }

        // Calculate next run time
        var cronExpression = new CronExpression(request.CronExpression);
        TimeZoneInfo timeZone;
        try
        {
            timeZone = TimeZoneInfo.FindSystemTimeZoneById(request.Timezone);
        }
        catch (TimeZoneNotFoundException)
        {
            throw new ArgumentException($"Invalid timezone: {request.Timezone}");
        }

        var nextFireTime = cronExpression.GetNextValidTimeAfter(DateTimeOffset.UtcNow);

        var schedule = new PipelineSchedule
        {
            Id = Guid.NewGuid(),
            PipelineId = request.PipelineId,
            TenantId = tenantId,
            CronExpression = request.CronExpression,
            Timezone = request.Timezone,
            Description = request.Description,
            IsActive = request.IsActive,
            IsPausedByPipeline = false,
            NextRunAt = nextFireTime,
            ConsecutiveFailures = 0,
            MaxConsecutiveFailures = 5,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = userId
        };

        // Generate Quartz keys
        schedule.QuartzJobKey = $"pipeline-{request.PipelineId}-{schedule.Id}";
        schedule.QuartzTriggerKey = $"trigger-{request.PipelineId}-{schedule.Id}";

        _context.PipelineSchedules.Add(schedule);
        await _context.SaveChangesAsync(cancellationToken);

        // Register with Quartz if active
        if (schedule.IsActive)
        {
            await RegisterQuartzJobAsync(schedule, pipeline, cancellationToken);
        }

        await _auditService.LogAsync(
            action: AuditActions.Schedules.Created,
            resourceType: "PipelineSchedule",
            resourceId: schedule.Id.ToString(),
            description: $"Created schedule for pipeline '{pipeline.Name}'",
            metadata: new { schedule.CronExpression, schedule.Timezone, schedule.IsActive }
        );

        _logger.LogInformation("Schedule {ScheduleId} created for pipeline {PipelineId}", schedule.Id, request.PipelineId);

        return await MapToResponseAsync(schedule, pipeline, cancellationToken);
    }

    public async Task<ScheduleResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tenantId = _currentUserService.GetTenantId();

        var schedule = await _context.PipelineSchedules
            .Include(s => s.Pipeline)
            .FirstOrDefaultAsync(s => s.Id == id && s.TenantId == tenantId, cancellationToken);

        if (schedule == null)
        {
            throw new KeyNotFoundException($"Schedule with ID {id} not found");
        }

        return await MapToResponseAsync(schedule, schedule.Pipeline, cancellationToken);
    }

    public async Task<ScheduleResponse?> GetByPipelineIdAsync(Guid pipelineId, CancellationToken cancellationToken = default)
    {
        var tenantId = _currentUserService.GetTenantId();

        var schedule = await _context.PipelineSchedules
            .Include(s => s.Pipeline)
            .FirstOrDefaultAsync(s => s.PipelineId == pipelineId && s.TenantId == tenantId, cancellationToken);

        if (schedule == null)
        {
            return null;
        }

        return await MapToResponseAsync(schedule, schedule.Pipeline, cancellationToken);
    }

    public async Task<PagedScheduleResponse> GetAllAsync(ScheduleSearchRequest request, CancellationToken cancellationToken = default)
    {
        var tenantId = _currentUserService.GetTenantId();

        var query = _context.PipelineSchedules
            .Include(s => s.Pipeline)
            .Where(s => s.TenantId == tenantId);

        // Apply filters
        if (request.PipelineId.HasValue)
        {
            query = query.Where(s => s.PipelineId == request.PipelineId.Value);
        }

        if (request.IsActive.HasValue)
        {
            query = query.Where(s => s.IsActive == request.IsActive.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            query = query.Where(s =>
                s.Pipeline!.Name.Contains(request.Search) ||
                (s.Description != null && s.Description.Contains(request.Search)));
        }

        // Apply sorting
        query = ApplySorting(query, request.SortBy);

        var totalCount = await query.CountAsync(cancellationToken);
        var totalPages = (int)Math.Ceiling(totalCount / (double)request.PageSize);

        var schedules = await query
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedScheduleResponse
        {
            Schedules = schedules.Select(s => new ScheduleListResponse
            {
                Id = s.Id,
                PipelineId = s.PipelineId,
                PipelineName = s.Pipeline?.Name,
                CronExpression = s.CronExpression,
                Timezone = s.Timezone,
                IsActive = s.IsActive,
                NextRunAt = s.NextRunAt,
                LastRunAt = s.Pipeline?.LastRunAt,
                LastRunStatus = s.Pipeline?.LastRunStatus,
                CronDescription = GetCronDescription(s.CronExpression),
                CreatedAt = s.CreatedAt
            }).ToList(),
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize,
            TotalPages = totalPages
        };
    }

    public async Task<ScheduleResponse> UpdateAsync(Guid id, UpdateScheduleRequest request, CancellationToken cancellationToken = default)
    {
        var tenantId = _currentUserService.GetTenantId();
        var userId = _currentUserService.GetUserId();

        var schedule = await _context.PipelineSchedules
            .Include(s => s.Pipeline)
            .FirstOrDefaultAsync(s => s.Id == id && s.TenantId == tenantId, cancellationToken);

        if (schedule == null)
        {
            throw new KeyNotFoundException($"Schedule with ID {id} not found");
        }

        // Validate new cron expression
        var validationResult = await ValidateCronExpressionAsync(request.CronExpression, request.Timezone, cancellationToken);
        if (!validationResult.IsValid)
        {
            throw new ArgumentException(validationResult.ErrorMessage);
        }

        var wasActive = schedule.IsActive;
        var cronChanged = schedule.CronExpression != request.CronExpression;
        var timezoneChanged = schedule.Timezone != request.Timezone;

        // Update schedule
        schedule.CronExpression = request.CronExpression;
        schedule.Timezone = request.Timezone;
        schedule.Description = request.Description;

        if (request.IsActive.HasValue)
        {
            // Check if trying to activate schedule for a deactivated pipeline
            if (request.IsActive.Value && schedule.Pipeline != null && !schedule.Pipeline.IsActive)
            {
                throw new InvalidOperationException($"Cannot activate schedule because pipeline '{schedule.Pipeline.Name}' is deactivated");
            }

            schedule.IsActive = request.IsActive.Value;
            schedule.IsPausedByPipeline = false;
        }

        schedule.UpdatedAt = DateTime.UtcNow;
        schedule.UpdatedBy = userId;

        // Recalculate next run time
        var cronExpression = new CronExpression(request.CronExpression);
        schedule.NextRunAt = cronExpression.GetNextValidTimeAfter(DateTimeOffset.UtcNow);

        // Reset consecutive failures if cron expression changed
        if (cronChanged || timezoneChanged)
        {
            schedule.ConsecutiveFailures = 0;
        }

        await _context.SaveChangesAsync(cancellationToken);

        // Update Quartz job
        if (schedule.IsActive)
        {
            if (!wasActive || cronChanged || timezoneChanged)
            {
                // Remove old job and create new one
                await UnregisterQuartzJobAsync(schedule, cancellationToken);
                await RegisterQuartzJobAsync(schedule, schedule.Pipeline!, cancellationToken);
            }
        }
        else if (wasActive)
        {
            // Was active but now disabled - remove job
            await UnregisterQuartzJobAsync(schedule, cancellationToken);
        }

        await _auditService.LogAsync(
            action: AuditActions.Schedules.Updated,
            resourceType: "PipelineSchedule",
            resourceId: schedule.Id.ToString(),
            description: $"Updated schedule for pipeline '{schedule.Pipeline?.Name}'",
            metadata: new { schedule.CronExpression, schedule.Timezone, schedule.IsActive }
        );

        _logger.LogInformation("Schedule {ScheduleId} updated", id);

        return await MapToResponseAsync(schedule, schedule.Pipeline, cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tenantId = _currentUserService.GetTenantId();

        var schedule = await _context.PipelineSchedules
            .Include(s => s.Pipeline)
            .FirstOrDefaultAsync(s => s.Id == id && s.TenantId == tenantId, cancellationToken);

        if (schedule == null)
        {
            throw new KeyNotFoundException($"Schedule with ID {id} not found");
        }

        var pipelineName = schedule.Pipeline?.Name;

        // Remove from Quartz
        await UnregisterQuartzJobAsync(schedule, cancellationToken);

        _context.PipelineSchedules.Remove(schedule);
        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            action: AuditActions.Schedules.Deleted,
            resourceType: "PipelineSchedule",
            resourceId: id.ToString(),
            description: $"Deleted schedule for pipeline '{pipelineName}'",
            metadata: new { PipelineId = schedule.PipelineId }
        );

        _logger.LogInformation("Schedule {ScheduleId} deleted", id);
    }

    public async Task<ScheduleResponse> EnableAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tenantId = _currentUserService.GetTenantId();
        var userId = _currentUserService.GetUserId();

        var schedule = await _context.PipelineSchedules
            .Include(s => s.Pipeline)
            .FirstOrDefaultAsync(s => s.Id == id && s.TenantId == tenantId, cancellationToken);

        if (schedule == null)
        {
            throw new KeyNotFoundException($"Schedule with ID {id} not found");
        }

        if (schedule.IsActive)
        {
            return await MapToResponseAsync(schedule, schedule.Pipeline, cancellationToken);
        }

        // Check if pipeline is active - cannot enable schedule for a deactivated pipeline
        if (schedule.Pipeline != null && !schedule.Pipeline.IsActive)
        {
            throw new InvalidOperationException($"Cannot enable schedule because pipeline '{schedule.Pipeline.Name}' is deactivated");
        }

        schedule.IsActive = true;
        schedule.IsPausedByPipeline = false;
        schedule.ConsecutiveFailures = 0; // Reset on enable
        schedule.UpdatedAt = DateTime.UtcNow;
        schedule.UpdatedBy = userId;

        // Recalculate next run time
        var cronExpression = new CronExpression(schedule.CronExpression);
        schedule.NextRunAt = cronExpression.GetNextValidTimeAfter(DateTimeOffset.UtcNow);

        await _context.SaveChangesAsync(cancellationToken);

        // Register with Quartz
        await RegisterQuartzJobAsync(schedule, schedule.Pipeline!, cancellationToken);

        await _auditService.LogAsync(
            action: AuditActions.Schedules.Enabled,
            resourceType: "PipelineSchedule",
            resourceId: id.ToString(),
            description: $"Enabled schedule for pipeline '{schedule.Pipeline?.Name}'",
            metadata: new { schedule.CronExpression }
        );

        _logger.LogInformation("Schedule {ScheduleId} enabled", id);

        return await MapToResponseAsync(schedule, schedule.Pipeline, cancellationToken);
    }

    public async Task<ScheduleResponse> DisableAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tenantId = _currentUserService.GetTenantId();
        var userId = _currentUserService.GetUserId();

        var schedule = await _context.PipelineSchedules
            .Include(s => s.Pipeline)
            .FirstOrDefaultAsync(s => s.Id == id && s.TenantId == tenantId, cancellationToken);

        if (schedule == null)
        {
            throw new KeyNotFoundException($"Schedule with ID {id} not found");
        }

        if (!schedule.IsActive && !schedule.IsPausedByPipeline)
        {
            return await MapToResponseAsync(schedule, schedule.Pipeline, cancellationToken);
        }

        schedule.IsActive = false;
        schedule.IsPausedByPipeline = false;
        schedule.UpdatedAt = DateTime.UtcNow;
        schedule.UpdatedBy = userId;

        await _context.SaveChangesAsync(cancellationToken);

        // Unregister from Quartz
        await UnregisterQuartzJobAsync(schedule, cancellationToken);

        await _auditService.LogAsync(
            action: AuditActions.Schedules.Disabled,
            resourceType: "PipelineSchedule",
            resourceId: id.ToString(),
            description: $"Disabled schedule for pipeline '{schedule.Pipeline?.Name}'",
            metadata: new { schedule.CronExpression }
        );

        _logger.LogInformation("Schedule {ScheduleId} disabled", id);

        return await MapToResponseAsync(schedule, schedule.Pipeline, cancellationToken);
    }

    public async Task<ScheduleResponse> TriggerNowAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tenantId = _currentUserService.GetTenantId();

        var schedule = await _context.PipelineSchedules
            .Include(s => s.Pipeline)
            .FirstOrDefaultAsync(s => s.Id == id && s.TenantId == tenantId, cancellationToken);

        if (schedule == null)
        {
            throw new KeyNotFoundException($"Schedule with ID {id} not found");
        }

        // Trigger the job immediately
        var scheduler = await _schedulerFactory.GetScheduler(cancellationToken);
        var jobKey = new JobKey(schedule.QuartzJobKey ?? $"pipeline-{schedule.PipelineId}-{schedule.Id}");

        // Check if job exists, if not create it temporarily
        if (!await scheduler.CheckExists(jobKey, cancellationToken))
        {
            await RegisterQuartzJobAsync(schedule, schedule.Pipeline!, cancellationToken);
        }

        await scheduler.TriggerJob(jobKey, cancellationToken);

        await _auditService.LogAsync(
            action: AuditActions.Schedules.TriggeredManually,
            resourceType: "PipelineSchedule",
            resourceId: id.ToString(),
            description: $"Manually triggered schedule for pipeline '{schedule.Pipeline?.Name}'",
            metadata: null
        );

        _logger.LogInformation("Schedule {ScheduleId} triggered manually", id);

        return await MapToResponseAsync(schedule, schedule.Pipeline, cancellationToken);
    }

    public Task<CronValidationResult> ValidateCronExpressionAsync(string cronExpression, string timezone, CancellationToken cancellationToken = default)
    {
        try
        {
            // Validate cron expression
            var cron = new CronExpression(cronExpression);

            // Validate timezone
            TimeZoneInfo timeZone;
            try
            {
                timeZone = TimeZoneInfo.FindSystemTimeZoneById(timezone);
            }
            catch (TimeZoneNotFoundException)
            {
                return Task.FromResult(new CronValidationResult
                {
                    IsValid = false,
                    ErrorMessage = $"Invalid timezone: {timezone}"
                });
            }

            // Get next 5 executions
            var nextExecutions = new List<DateTimeOffset>();
            var currentTime = DateTimeOffset.UtcNow;

            for (int i = 0; i < 5; i++)
            {
                var nextTime = cron.GetNextValidTimeAfter(currentTime);
                if (nextTime.HasValue)
                {
                    nextExecutions.Add(nextTime.Value);
                    currentTime = nextTime.Value;
                }
                else
                {
                    break;
                }
            }

            return Task.FromResult(new CronValidationResult
            {
                IsValid = true,
                Description = GetCronDescription(cronExpression),
                NextExecutions = nextExecutions
            });
        }
        catch (FormatException ex)
        {
            return Task.FromResult(new CronValidationResult
            {
                IsValid = false,
                ErrorMessage = $"Invalid cron expression: {ex.Message}"
            });
        }
    }

    public async Task PauseSchedulesForPipelineAsync(Guid pipelineId, CancellationToken cancellationToken = default)
    {
        var tenantId = _currentUserService.GetTenantId();

        _logger.LogInformation("Pausing schedules for pipeline {PipelineId} in tenant {TenantId}",
            pipelineId, tenantId);

        // Find all active schedules for this pipeline
        var schedules = await _context.PipelineSchedules
            .Include(s => s.Pipeline)
            .Where(s => s.PipelineId == pipelineId && s.TenantId == tenantId && s.IsActive)
            .ToListAsync(cancellationToken);

        if (schedules.Count == 0)
        {
            _logger.LogInformation("No active schedules found for pipeline {PipelineId}", pipelineId);
            return;
        }

        // Get the pipeline name for audit logging (safe since we have at least one schedule)
        var pipelineName = schedules[0].Pipeline?.Name ?? "Unknown";

        foreach (var schedule in schedules)
        {
            // Unregister from Quartz
            await UnregisterQuartzJobAsync(schedule, cancellationToken);

            // Mark the schedule as pipeline-paused so we only restore schedules
            // that were active before the pipeline was deactivated.
            schedule.IsActive = false;
            schedule.IsPausedByPipeline = true;
            schedule.UpdatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            action: AuditActions.Schedules.PausedForPipeline,
            resourceType: "Pipeline",
            resourceId: pipelineId.ToString(),
            description: $"Paused {schedules.Count} schedule(s) for pipeline '{pipelineName}' due to pipeline deactivation",
            metadata: new { ScheduleIds = schedules.Select(s => s.Id).ToList(), Count = schedules.Count }
        );

        _logger.LogInformation("Paused {Count} schedule(s) for pipeline {PipelineId}", schedules.Count, pipelineId);
    }

    public async Task ResumeSchedulesForPipelineAsync(Guid pipelineId, CancellationToken cancellationToken = default)
    {
        var tenantId = _currentUserService.GetTenantId();

        _logger.LogInformation("Resuming schedules for pipeline {PipelineId} in tenant {TenantId}",
            pipelineId, tenantId);

        // Only resume schedules that were auto-paused due to pipeline deactivation.
        var schedules = await _context.PipelineSchedules
            .Include(s => s.Pipeline)
            .Where(s => s.PipelineId == pipelineId &&
                        s.TenantId == tenantId &&
                        s.IsPausedByPipeline &&
                        !s.IsActive)
            .ToListAsync(cancellationToken);

        if (schedules.Count == 0)
        {
            _logger.LogInformation("No pipeline-paused schedules found for pipeline {PipelineId} to resume", pipelineId);
            return;
        }

        // Get the pipeline name for audit logging (safe since we have at least one schedule)
        var pipelineName = schedules[0].Pipeline?.Name ?? "Unknown";

        foreach (var schedule in schedules)
        {
            // Recalculate next run time
            var cronExpression = new CronExpression(schedule.CronExpression);
            schedule.NextRunAt = cronExpression.GetNextValidTimeAfter(DateTimeOffset.UtcNow);

            // Set schedule to active
            schedule.IsActive = true;
            schedule.IsPausedByPipeline = false;
            schedule.UpdatedAt = DateTime.UtcNow;

            // Pipeline should not be null after Include, but check for safety
            if (schedule.Pipeline != null)
            {
                await RegisterQuartzJobAsync(schedule, schedule.Pipeline, cancellationToken);
            }
            else
            {
                _logger.LogWarning("Pipeline is null for schedule {ScheduleId}, skipping Quartz registration", schedule.Id);
            }
        }

        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            action: AuditActions.Schedules.ResumedForPipeline,
            resourceType: "Pipeline",
            resourceId: pipelineId.ToString(),
            description: $"Resumed {schedules.Count} schedule(s) for pipeline '{pipelineName}' due to pipeline activation",
            metadata: new { ScheduleIds = schedules.Select(s => s.Id).ToList(), Count = schedules.Count }
        );

        _logger.LogInformation("Resumed {Count} schedule(s) for pipeline {PipelineId}", schedules.Count, pipelineId);
    }

    private async Task RegisterQuartzJobAsync(PipelineSchedule schedule, Pipeline pipeline, CancellationToken cancellationToken)
    {
        var scheduler = await _schedulerFactory.GetScheduler(cancellationToken);

        var jobKey = new JobKey(schedule.QuartzJobKey ?? $"pipeline-{schedule.PipelineId}-{schedule.Id}");
        var triggerKey = new TriggerKey(schedule.QuartzTriggerKey ?? $"trigger-{schedule.PipelineId}-{schedule.Id}");

        // Create job detail
        var jobDetail = JobBuilder.Create<PipelineScheduleJob>()
            .WithIdentity(jobKey)
            .WithDescription($"Scheduled execution for pipeline: {pipeline.Name}")
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
        }

        var trigger = TriggerBuilder.Create()
            .WithIdentity(triggerKey)
            .ForJob(jobKey)
            .WithCronSchedule(schedule.CronExpression, x => x.InTimeZone(timeZone))
            .WithDescription($"Cron trigger: {schedule.CronExpression} ({schedule.Timezone})")
            .Build();

        // Schedule the job
        if (await scheduler.CheckExists(jobKey, cancellationToken))
        {
            await scheduler.DeleteJob(jobKey, cancellationToken);
        }

        await scheduler.ScheduleJob(jobDetail, trigger, cancellationToken);

        _logger.LogInformation(
            "Registered Quartz job for schedule {ScheduleId}, next fire time: {NextFireTime}",
            schedule.Id, trigger.GetNextFireTimeUtc());
    }

    private async Task UnregisterQuartzJobAsync(PipelineSchedule schedule, CancellationToken cancellationToken)
    {
        var scheduler = await _schedulerFactory.GetScheduler(cancellationToken);
        var jobKey = new JobKey(schedule.QuartzJobKey ?? $"pipeline-{schedule.PipelineId}-{schedule.Id}");

        if (await scheduler.CheckExists(jobKey, cancellationToken))
        {
            await scheduler.DeleteJob(jobKey, cancellationToken);
            _logger.LogInformation("Unregistered Quartz job for schedule {ScheduleId}", schedule.Id);
        }
    }

    private static IQueryable<PipelineSchedule> ApplySorting(IQueryable<PipelineSchedule> query, string? sortBy)
    {
        return sortBy switch
        {
            "next_run_asc" => query.OrderBy(s => s.NextRunAt ?? DateTimeOffset.MaxValue),
            "next_run_desc" => query.OrderByDescending(s => s.NextRunAt ?? DateTimeOffset.MinValue),
            "created_asc" => query.OrderBy(s => s.CreatedAt),
            "created_desc" => query.OrderByDescending(s => s.CreatedAt),
            "pipeline_asc" => query.OrderBy(s => s.Pipeline!.Name),
            "pipeline_desc" => query.OrderByDescending(s => s.Pipeline!.Name),
            _ => query.OrderByDescending(s => s.CreatedAt)
        };
    }

    private Task<ScheduleResponse> MapToResponseAsync(PipelineSchedule schedule, Pipeline? pipeline, CancellationToken cancellationToken)
    {
        var response = new ScheduleResponse
        {
            Id = schedule.Id,
            PipelineId = schedule.PipelineId,
            PipelineName = pipeline?.Name,
            TenantId = schedule.TenantId,
            CronExpression = schedule.CronExpression,
            Timezone = schedule.Timezone,
            Description = schedule.Description,
            IsActive = schedule.IsActive,
            NextRunAt = schedule.NextRunAt,
            LastRunAt = pipeline?.LastRunAt,
            LastRunStatus = pipeline?.LastRunStatus,
            ConsecutiveFailures = schedule.ConsecutiveFailures,
            MaxConsecutiveFailures = schedule.MaxConsecutiveFailures,
            CronDescription = GetCronDescription(schedule.CronExpression),
            CreatedAt = schedule.CreatedAt,
            UpdatedAt = schedule.UpdatedAt
        };

        return Task.FromResult(response);
    }

    /// <summary>
    /// Provides a simple human-readable description for common cron patterns.
    /// For more complex expressions, returns the cron expression itself.
    /// Consider using CronExpressionDescriptor library for production if detailed descriptions are needed.
    /// </summary>
    public static string GetCronDescription(string cronExpression)
    {
        try
        {
            var parts = cronExpression.Split(' ');
            if (parts.Length < 5) return cronExpression;

            // Handle both 5-field (minute hour day month weekday) and 6-field (second minute hour day month weekday) cron
            var minute = parts.Length > 5 ? parts[1] : parts[0];
            var hour = parts.Length > 5 ? parts[2] : parts[1];
            var dayOfMonth = parts.Length > 5 ? parts[3] : parts[2];
            var month = parts.Length > 5 ? parts[4] : parts[3];
            var dayOfWeek = parts.Length > 5 ? parts[5] : parts[4];

            // Match exact common patterns only
            if (minute == "0" && hour == "0" && dayOfMonth == "*" && month == "*" && (dayOfWeek == "*" || dayOfWeek == "?"))
                return "Daily at midnight";
            if (minute == "0" && hour == "*" && dayOfMonth == "*" && month == "*" && (dayOfWeek == "*" || dayOfWeek == "?"))
                return "Every hour at :00";
            if (minute == "*/5" && hour == "*" && dayOfMonth == "*" && month == "*" && (dayOfWeek == "*" || dayOfWeek == "?"))
                return "Every 5 minutes";
            if (minute == "*/15" && hour == "*" && dayOfMonth == "*" && month == "*" && (dayOfWeek == "*" || dayOfWeek == "?"))
                return "Every 15 minutes";
            if (minute == "*/30" && hour == "*" && dayOfMonth == "*" && month == "*" && (dayOfWeek == "*" || dayOfWeek == "?"))
                return "Every 30 minutes";

            return cronExpression;
        }
        catch
        {
            return cronExpression;
        }
    }
}
