namespace MultiTenantETL.Application.Connectors.Queries;

/// <summary>
/// Query to get a connector by ID
/// </summary>
public record GetConnectorByIdQuery(
    Guid Id,
    Guid TenantId);
