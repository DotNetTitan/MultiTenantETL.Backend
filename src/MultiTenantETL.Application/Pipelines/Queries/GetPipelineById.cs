namespace MultiTenantETL.Application.Pipelines.Queries;

/// <summary>
/// Query to get a pipeline by ID
/// </summary>
public record GetPipelineByIdQuery(
    Guid Id,
    Guid TenantId);
