namespace MultiTenantETL.Application.Pipelines.Commands;

/// <summary>
/// Command to toggle pipeline active status
/// </summary>
public record TogglePipelineStatusCommand(
    Guid Id,
    Guid TenantId,
    Guid UserId);
