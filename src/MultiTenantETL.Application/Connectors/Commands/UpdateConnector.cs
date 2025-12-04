using System.Text.Json;
using Wolverine;

namespace MultiTenantETL.Application.Connectors.Commands;

/// <summary>
/// Command to update an existing connector
/// </summary>
public record UpdateConnectorCommand(
    Guid Id,
    string Name,
    string? Description,
    string Direction,
    JsonElement Config,
    JsonElement? Schema,
    bool? IsActive,
    Guid TenantId,
    Guid UserId);
