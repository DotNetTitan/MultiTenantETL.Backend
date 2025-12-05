using MultiTenantETL.Application.Scheduling.Models;

namespace MultiTenantETL.Application.Scheduling;

/// <summary>
/// Service interface for managing pipeline schedules
/// </summary>
public interface IScheduleService
{
    /// <summary>
    /// Creates a new schedule for a pipeline
    /// </summary>
    Task<ScheduleResponse> CreateAsync(CreateScheduleRequest request, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets a schedule by ID
    /// </summary>
    Task<ScheduleResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets the schedule for a specific pipeline
    /// </summary>
    Task<ScheduleResponse?> GetByPipelineIdAsync(Guid pipelineId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets all schedules with optional filtering and pagination
    /// </summary>
    Task<PagedScheduleResponse> GetAllAsync(ScheduleSearchRequest request, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Updates an existing schedule
    /// </summary>
    Task<ScheduleResponse> UpdateAsync(Guid id, UpdateScheduleRequest request, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Deletes a schedule
    /// </summary>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Enables a schedule
    /// </summary>
    Task<ScheduleResponse> EnableAsync(Guid id, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Disables a schedule
    /// </summary>
    Task<ScheduleResponse> DisableAsync(Guid id, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Triggers an immediate execution of a scheduled pipeline (manual trigger)
    /// </summary>
    Task<ScheduleResponse> TriggerNowAsync(Guid id, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Validates a cron expression
    /// </summary>
    Task<CronValidationResult> ValidateCronExpressionAsync(string cronExpression, string timezone, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Pauses all active schedules for a pipeline by unregistering Quartz jobs.
    /// Called when a pipeline is disabled. Does not change Schedule.IsActive in the database.
    /// </summary>
    Task PauseSchedulesForPipelineAsync(Guid pipelineId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Resumes schedules for a pipeline by re-registering Quartz jobs.
    /// Called when a pipeline is re-enabled. Only resumes schedules where Schedule.IsActive is true.
    /// </summary>
    Task ResumeSchedulesForPipelineAsync(Guid pipelineId, CancellationToken cancellationToken = default);
}
