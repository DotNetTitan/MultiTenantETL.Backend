namespace MultiTenantETL.Application.Orchestration;

/// <summary>
/// Orchestrates the execution of a pipeline (Extract -> Transform -> Load)
/// </summary>
public interface IPipelineOrchestrator
{
    /// <summary>
    /// Executes a pipeline end-to-end
    /// </summary>
    Task ExecutePipelineAsync(Guid executionId, CancellationToken cancellationToken = default);
}
