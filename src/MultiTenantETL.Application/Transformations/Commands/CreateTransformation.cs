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
