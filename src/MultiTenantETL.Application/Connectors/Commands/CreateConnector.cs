using System.Text.Json;
using Wolverine;

namespace MultiTenantETL.Application.Connectors.Commands;

/// <summary>
/// Command to create a new connector
/// </summary>
public record CreateConnectorCommand(
    string Name,
    string? Description,
    string Type,
    string Provider,
    string Direction,
    JsonElement Config,
    JsonElement? Schema,
    Guid TenantId,
    Guid UserId);

/// <summary>
/// Response from creating a connector
/// </summary>
public record CreateConnectorResponse(
    Guid Id,
    string Name,
    string Type,
    string Provider,
    string Direction,
    bool IsActive,
    DateTime CreatedAt);
