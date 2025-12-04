namespace MultiTenantETL.Application.Connectors.Queries;

/// <summary>
/// Query to search connectors with pagination and filtering
/// </summary>
public record SearchConnectorsQuery(
    string? Name,
    string? Type,
    string? Provider,
    string? Direction,
    bool? IsActive,
    int Page,
    int PageSize,
    Guid TenantId);

/// <summary>
/// Query to get all active connectors for a tenant
/// </summary>
public record GetAllConnectorsQuery(
    Guid TenantId);
