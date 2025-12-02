using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace MultiTenantETL.Application.Pipelines.Models;

public record CreatePipelineRequest
{
    [Required]
    [StringLength(200, MinimumLength = 2)]
    public required string Name { get; init; }

    [StringLength(500)]
    public string? Description { get; init; }

    [Required]
    public Guid SourceConnectorId { get; init; }

    [Required]
    public Guid DestinationConnectorId { get; init; }

    [Required]
    public required JsonElement FieldMappings { get; init; } // Array of field mappings

    public JsonElement? Schedule { get; init; }

    public bool IsScheduled { get; init; }
}

public record UpdatePipelineRequest
{
    [Required]
    [StringLength(200, MinimumLength = 2)]
    public required string Name { get; init; }

    [StringLength(500)]
    public string? Description { get; init; }

    [Required]
    public required JsonElement FieldMappings { get; init; }

    public JsonElement? Schedule { get; init; }

    public bool IsScheduled { get; init; }

    public bool? IsActive { get; init; }
}

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
    public JsonElement? Schedule { get; init; }
    public bool IsScheduled { get; init; }
    public bool IsActive { get; init; }
    public DateTime? LastRunAt { get; init; }
    public string? LastRunStatus { get; init; }
    public int? LastRunRecordsProcessed { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
}

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

public record PagedPipelineResponse
{
    public List<PipelineListResponse> Pipelines { get; init; } = new();
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalPages { get; init; }
}

// Configuration models for pipeline components
public record FieldMapping
{
    public required string Id { get; init; }
    public List<string> SourceFields { get; init; } = new();
    public required string DestinationField { get; init; }
    public List<FieldTransformation> Transformations { get; init; } = new();
    public int Order { get; init; }
}

public record FieldTransformation
{
    public required string Id { get; init; }
    public required string Type { get; init; } // Filter, Map, Trim, CaseConvert, Substring, Replace, Script
    public required JsonElement Config { get; init; } // Type-specific configuration
    public int Order { get; init; }
    public bool IsEnabled { get; init; } = true;
}

public record PipelineSchedule
{
    public required string Frequency { get; init; } // Daily, Weekly, Monthly, Custom
    public string? Time { get; init; } // HH:mm format
    public int? DayOfWeek { get; init; } // 0-6 for Weekly
    public int? DayOfMonth { get; init; } // 1-31 for Monthly
    public string? CronExpression { get; init; } // For Custom
    public required string Timezone { get; init; }
}
