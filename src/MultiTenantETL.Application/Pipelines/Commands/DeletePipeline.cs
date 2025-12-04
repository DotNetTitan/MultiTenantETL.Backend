namespace MultiTenantETL.Application.Pipelines.Commands;

/// <summary>
/// Command to delete a pipeline
/// </summary>
public record DeletePipelineCommand(
    Guid Id,
    Guid TenantId);
