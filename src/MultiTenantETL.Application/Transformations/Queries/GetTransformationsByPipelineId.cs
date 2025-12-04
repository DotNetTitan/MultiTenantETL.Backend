namespace MultiTenantETL.Application.Transformations.Queries;

/// <summary>
/// Query to get transformations by pipeline ID
/// </summary>
public record GetTransformationsByPipelineIdQuery(
    Guid PipelineId,
    Guid TenantId);
