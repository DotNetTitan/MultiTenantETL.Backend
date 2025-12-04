using System.Text.Json;

namespace MultiTenantETL.Application.Pipelines.Commands;

/// <summary>
/// Command to update an existing pipeline
/// </summary>
public record UpdatePipelineCommand(
    Guid Id,
    string Name,
    string? Description,
    JsonElement FieldMappings,
    JsonElement? Schedule,
    bool IsScheduled,
    bool? IsActive,
    Guid TenantId,
    Guid UserId);
