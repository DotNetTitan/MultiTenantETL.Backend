using MultiTenantETL.Domain.Interfaces;

namespace MultiTenantETL.Domain.Entities;

/// <summary>
/// Represents a schedule for automated pipeline execution.
/// Each pipeline can have one active schedule at a time.
/// </summary>
public class PipelineSchedule : ITenantResource
{
    public Guid Id { get; set; }
    public Guid PipelineId { get; set; }
    public Guid TenantId { get; set; }
    
    /// <summary>
    /// Cron expression defining the schedule (e.g., "0 0 * * *" for daily at midnight)
    /// </summary>
    public required string CronExpression { get; set; }
    
    /// <summary>
    /// IANA timezone identifier (e.g., "America/New_York", "Europe/London", "UTC")
    /// </summary>
    public required string Timezone { get; set; }
    
    /// <summary>
    /// Whether this schedule is currently active and should trigger executions
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// Indicates the schedule was auto-paused because its pipeline was deactivated.
    /// This preserves the difference between pipeline pauses and manual user disables.
    /// </summary>
    public bool IsPausedByPipeline { get; set; }
    
    /// <summary>
    /// Human-readable description of the schedule
    /// </summary>
    public string? Description { get; set; }
    
    /// <summary>
    /// When the next execution is scheduled to occur
    /// </summary>
    public DateTimeOffset? NextRunAt { get; set; }
    
    /// <summary>
    /// Count of consecutive failures - used to disable after threshold
    /// </summary>
    public int ConsecutiveFailures { get; set; }
    
    /// <summary>
    /// Maximum consecutive failures before auto-disabling (default 5)
    /// </summary>
    public int MaxConsecutiveFailures { get; set; } = 5;
    
    /// <summary>
    /// Quartz job key for identifying this schedule's job
    /// </summary>
    public string? QuartzJobKey { get; set; }
    
    /// <summary>
    /// Quartz trigger key for identifying this schedule's trigger
    /// </summary>
    public string? QuartzTriggerKey { get; set; }
    
    // Audit fields
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid CreatedBy { get; set; }
    public Guid? UpdatedBy { get; set; }

    // Navigation properties
    public Pipeline? Pipeline { get; set; }
    public Tenant? Tenant { get; set; }
}
