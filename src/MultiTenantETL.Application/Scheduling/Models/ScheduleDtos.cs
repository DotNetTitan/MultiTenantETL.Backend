using System.ComponentModel.DataAnnotations;

namespace MultiTenantETL.Application.Scheduling.Models;

/// <summary>
/// Request to create a new schedule for a pipeline.
/// </summary>
public record CreateScheduleRequest
{
    [Required]
    public Guid PipelineId { get; init; }
    
    [Required]
    [StringLength(100, MinimumLength = 9)]
    public required string CronExpression { get; init; }
    
    [Required]
    [StringLength(50)]
    public required string Timezone { get; init; }
    
    [StringLength(500)]
    public string? Description { get; init; }
    
    public bool IsActive { get; init; } = true;
}

/// <summary>
/// Request to update an existing schedule.
/// </summary>
public record UpdateScheduleRequest
{
    [Required]
    [StringLength(100, MinimumLength = 9)]
    public required string CronExpression { get; init; }
    
    [Required]
    [StringLength(50)]
    public required string Timezone { get; init; }
    
    [StringLength(500)]
    public string? Description { get; init; }
    
    public bool? IsActive { get; init; }
}

/// <summary>
/// Response containing full schedule details.
/// </summary>
public record ScheduleResponse
{
    public Guid Id { get; init; }
    public Guid PipelineId { get; init; }
    public string? PipelineName { get; init; }
    public Guid TenantId { get; init; }
    public required string CronExpression { get; init; }
    public required string Timezone { get; init; }
    public string? Description { get; init; }
    public bool IsActive { get; init; }
    public DateTimeOffset? NextRunAt { get; init; }
    public DateTime? LastRunAt { get; init; }
    public string? LastRunStatus { get; init; }
    public int ConsecutiveFailures { get; init; }
    public int MaxConsecutiveFailures { get; init; }
    public string? CronDescription { get; init; } // Human-readable cron description
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
}

/// <summary>
/// Summary response for a schedule in lists.
/// </summary>
public record ScheduleListResponse
{
    public Guid Id { get; init; }
    public Guid PipelineId { get; init; }
    public string? PipelineName { get; init; }
    public required string CronExpression { get; init; }
    public required string Timezone { get; init; }
    public bool IsActive { get; init; }
    public DateTimeOffset? NextRunAt { get; init; }
    public DateTime? LastRunAt { get; init; }
    public string? LastRunStatus { get; init; }
    public string? CronDescription { get; init; }
    public DateTime CreatedAt { get; init; }
}

/// <summary>
/// Request to search schedules with filtering and pagination.
/// </summary>
public record ScheduleSearchRequest
{
    public Guid? PipelineId { get; init; }
    public bool? IsActive { get; init; }
    public string? Search { get; init; }
    public string? SortBy { get; init; } // next_run_asc, next_run_desc, created_desc, pipeline_asc
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}

/// <summary>
/// Paginated response containing schedule list with pagination metadata.
/// </summary>
public record PagedScheduleResponse
{
    public List<ScheduleListResponse> Schedules { get; init; } = new();
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalPages { get; init; }
}

public record CronValidationResult
{
    public bool IsValid { get; init; }
    public string? ErrorMessage { get; init; }
    public string? Description { get; init; } // Human-readable description
    public List<DateTimeOffset> NextExecutions { get; init; } = new(); // Next 5 executions
}
