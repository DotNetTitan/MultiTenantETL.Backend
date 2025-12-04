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

/// <summary>
/// Command to delete a pipeline
/// </summary>
public record DeletePipelineCommand(
    Guid Id,
    Guid TenantId);

/// <summary>
/// Command to toggle pipeline active status
/// </summary>
public record TogglePipelineStatusCommand(
    Guid Id,
    Guid TenantId,
    Guid UserId);

/// <summary>
/// Command to execute a pipeline
/// </summary>
public record ExecutePipelineCommand(
    Guid Id,
    Guid TenantId,
    Guid UserId);
