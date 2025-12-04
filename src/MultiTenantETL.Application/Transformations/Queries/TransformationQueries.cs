namespace MultiTenantETL.Application.Transformations.Queries;

/// <summary>
/// Query to get a transformation by ID
/// </summary>
public record GetTransformationByIdQuery(
    Guid Id,
    Guid TenantId);

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

/// <summary>
/// Query to get transformations by pipeline ID
/// </summary>
public record GetTransformationsByPipelineIdQuery(
    Guid PipelineId,
    Guid TenantId);
