namespace MultiTenantETL.Application.Connectors.Commands;

/// <summary>
/// Command to delete a connector
/// </summary>
public record DeleteConnectorCommand(
    Guid Id,
    Guid TenantId);
