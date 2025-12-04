namespace MultiTenantETL.Application.Transformations.Queries;

/// <summary>
/// Query to search transformations with pagination and filtering
/// </summary>
public record SearchTransformationsQuery(
    string? Name,
    string? Type,
    string? Search,
    string? Sort,
    int Page,
    int PageSize,
    Guid TenantId);
