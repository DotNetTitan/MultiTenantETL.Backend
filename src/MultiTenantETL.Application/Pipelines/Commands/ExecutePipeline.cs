namespace MultiTenantETL.Application.Pipelines.Commands;

/// <summary>
/// Command to execute a pipeline
/// </summary>
public record ExecutePipelineCommand(
    Guid Id,
    Guid TenantId,
    Guid UserId);
