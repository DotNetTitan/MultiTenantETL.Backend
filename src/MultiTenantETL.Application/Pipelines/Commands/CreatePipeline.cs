using System.Text.Json;

namespace MultiTenantETL.Application.Pipelines.Commands;

/// <summary>
/// Command to create a new pipeline
/// </summary>
public record CreatePipelineCommand(
    string Name,
    string? Description,
    Guid SourceConnectorId,
    Guid DestinationConnectorId,
    JsonElement FieldMappings,
    JsonElement? Schedule,
    bool IsScheduled,
    Guid TenantId,
    Guid UserId);
