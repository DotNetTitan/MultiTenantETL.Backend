using System.Text.Json;

namespace MultiTenantETL.Application.Connectors.Commands;

/// <summary>
/// Command to test a new connector configuration
/// </summary>
public record TestConnectionCommand(
    string Type,
    string Provider,
    JsonElement Config,
    Guid TenantId);

/// <summary>
/// Command to test an existing connector's connection
/// </summary>
public record TestExistingConnectionCommand(
    Guid Id,
    Guid TenantId);
