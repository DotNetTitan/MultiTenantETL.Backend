using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace MultiTenantETL.Application.Transformations.Models;

public record CreateTransformationRequest
{
    [Required]
    [StringLength(200, MinimumLength = 2)]
    public required string Name { get; init; }

    [StringLength(500)]
    public string? Description { get; init; }

    [Required]
    [StringLength(50)]
    public required string Type { get; init; } // Filter, Map, Trim, Case Convert, Substring, Replace, Script

    [Required]
    public required JsonElement Config { get; init; } // Type-specific configuration
}

public record UpdateTransformationRequest
{
    [Required]
    [StringLength(200, MinimumLength = 2)]
    public required string Name { get; init; }

    [StringLength(500)]
    public string? Description { get; init; }

    [Required]
    public required JsonElement Config { get; init; }
}

public record TransformationResponse
{
    public Guid Id { get; init; }
    public Guid TenantId { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public required string Type { get; init; }
    public required JsonElement Config { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
}

public record TransformationListResponse
{
    public Guid Id { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public required string Type { get; init; }
    public DateTime CreatedAt { get; init; }
}

public record TransformationSearchRequest
{
    public string? Name { get; init; }
    public string? Type { get; init; }
    public string? Search { get; init; }
    public string? Sort { get; init; } // name_asc, name_desc, type_asc, created_desc, created_asc
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}

public record PagedTransformationResponse
{
    public List<TransformationListResponse> Transformations { get; init; } = new();
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalPages { get; init; }
}

// Configuration models for different transformation types
public record FilterConfig
{
    public required string Operator { get; init; } // equals, contains, greaterThan, lessThan, startsWith, endsWith, etc.
    public required string ValueType { get; init; } // string, number, boolean, date
    public string? DefaultValue { get; init; }
}

public record MapConfig
{
    public List<MappingRule>? Mappings { get; init; }
    public string? DefaultValue { get; init; }
}

public record MappingRule
{
    public required string From { get; init; }
    public required string To { get; init; }
}

public record TrimConfig
{
    public List<string>? Fields { get; init; }
}

public record CaseConvertConfig
{
    public required string Field { get; init; }
    public required string CaseType { get; init; } // uppercase, lowercase, titlecase, camelcase
}

public record SubstringConfig
{
    public required string Field { get; init; }
    public int StartIndex { get; init; }
    public int? Length { get; init; }
}

public record ReplaceConfig
{
    public required string Field { get; init; }
    public required string FindPattern { get; init; }
    public required string ReplaceWith { get; init; }
    public bool UseRegex { get; init; }
}

public record ScriptConfig
{
    public required string ScriptLanguage { get; init; } // javascript, csharp
    public required string Script { get; init; }
}
