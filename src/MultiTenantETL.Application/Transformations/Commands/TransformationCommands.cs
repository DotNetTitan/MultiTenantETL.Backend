using System.Text.Json;

namespace MultiTenantETL.Application.Transformations.Commands;

/// <summary>
/// Command to create a new transformation
/// </summary>
public record CreateTransformationCommand(
    string Name,
    string? Description,
    string Type,
    JsonElement Config,
    Guid TenantId,
    Guid UserId);

/// <summary>
/// Command to update an existing transformation
/// </summary>
public record UpdateTransformationCommand(
    Guid Id,
    string Name,
    string? Description,
    JsonElement Config,
    Guid TenantId,
    Guid UserId);

/// <summary>
/// Command to delete a transformation
/// </summary>
public record DeleteTransformationCommand(
    Guid Id,
    Guid TenantId);
