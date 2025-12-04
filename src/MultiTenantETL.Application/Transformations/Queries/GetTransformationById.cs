namespace MultiTenantETL.Application.Transformations.Queries;

/// <summary>
/// Query to get a transformation by ID
/// </summary>
public record GetTransformationByIdQuery(
    Guid Id,
    Guid TenantId);
