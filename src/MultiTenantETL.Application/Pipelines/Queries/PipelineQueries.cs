namespace MultiTenantETL.Application.Pipelines.Queries;

/// <summary>
/// Query to get a pipeline by ID
/// </summary>
public record GetPipelineByIdQuery(
    Guid Id,
    Guid TenantId);

/// <summary>
/// Query to search pipelines with pagination and filtering
/// </summary>
public record SearchPipelinesQuery(
    string? Name,
    string? Status,
    string? Search,
    bool? IsScheduled,
    bool? IsActive,
    string? SortBy,
    int Page,
    int PageSize,
    Guid TenantId);
