using MultiTenantETL.Application.Scheduling.Models;
using System.Text.Json;

namespace MultiTenantETL.Application.Pipelines.Models;

/// <summary>
/// Request to create a new pipeline.
/// </summary>
public record CreatePipelineRequest
{
    public required string Name { get; init; }

    public string? Description { get; init; }

    public Guid SourceConnectorId { get; init; }

    public Guid DestinationConnectorId { get; init; }

    public required JsonElement FieldMappings { get; init; } // Array of field mappings

    public List<string>? NotificationEmails { get; init; } // Email addresses to notify after execution

    public bool EmailNotificationsEnabled { get; init; } = true; // Email notifications enabled by default
}

/// <summary>
/// Request to update an existing pipeline.
/// </summary>
public record UpdatePipelineRequest
{
    public required string Name { get; init; }

    public string? Description { get; init; }

    public required JsonElement FieldMappings { get; init; }

    public bool? IsActive { get; init; }

    public List<string>? NotificationEmails { get; init; } // Email addresses to notify after execution

    public bool? EmailNotificationsEnabled { get; init; } // Nullable to allow partial updates
}

/// <summary>
/// Response containing full pipeline details.
/// </summary>
public record PipelineResponse
{
    public Guid Id { get; init; }
    public Guid TenantId { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public Guid SourceConnectorId { get; init; }
    public string? SourceConnectorName { get; init; }
    public Guid DestinationConnectorId { get; init; }
    public string? DestinationConnectorName { get; init; }
    public required string Status { get; init; }
    public required JsonElement FieldMappings { get; init; }
    public bool IsScheduled { get; init; }
    public ScheduleResponse? Schedule { get; init; }
    public bool IsActive { get; init; }
    public List<string>? NotificationEmails { get; init; }
    public bool EmailNotificationsEnabled { get; init; }
    public DateTime? LastRunAt { get; init; }
    public string? LastRunStatus { get; init; }
    public int? LastRunRecordsProcessed { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
}

/// <summary>
/// Summary response for a pipeline in lists.
/// </summary>
public record PipelineListResponse
{
    public Guid Id { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public string? SourceConnectorName { get; init; }
    public string? DestinationConnectorName { get; init; }
    public required string Status { get; init; }
    public bool IsScheduled { get; init; }
    public bool IsActive { get; init; }
    public DateTime? LastRunAt { get; init; }
    public string? LastRunStatus { get; init; }
    public DateTime CreatedAt { get; init; }
}

/// <summary>
/// Request to search pipelines with filtering and pagination.
/// </summary>
public record PipelineSearchRequest
{
    public string? Name { get; init; }
    public string? Status { get; init; }
    public string? Search { get; init; }
    public bool? IsScheduled { get; init; }
    public bool? IsActive { get; init; }
    public string? SortBy { get; init; } // name_asc, name_desc, created_desc, lastRun_desc
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}

/// <summary>
/// Paginated response containing pipeline list with pagination metadata.
/// </summary>
public record PagedPipelineResponse
{
    public List<PipelineListResponse> Pipelines { get; init; } = new();
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalPages { get; init; }
}

/// <summary>
/// Represents a field mapping between source and destination in a pipeline.
/// </summary>
public record FieldMapping
{
    public required string Id { get; init; }
    public List<string> SourceFields { get; init; } = new();
    public required string DestinationField { get; init; }
    public List<FieldTransformation> Transformations { get; init; } = new();
    public int Order { get; init; }
}

/// <summary>
/// Represents a transformation to apply to a field during pipeline execution.
/// </summary>
public record FieldTransformation
{
    public required string Id { get; init; }
    public required string Type { get; init; } // Filter, Map, Trim, CaseConvert, Substring, Replace, Script
    public required JsonElement Config { get; init; } // Type-specific configuration
    public int Order { get; init; }
    public bool IsEnabled { get; init; } = true;
}
