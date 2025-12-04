using System.Text.Json;

namespace MultiTenantETL.Application.Transformations.Commands;

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
