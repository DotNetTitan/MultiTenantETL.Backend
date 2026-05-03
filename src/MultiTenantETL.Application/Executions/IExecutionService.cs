using MultiTenantETL.Application.Executions.Models;

namespace MultiTenantETL.Application.Executions;

/// <summary>
/// Service for managing pipeline executions.
/// </summary>
public interface IExecutionService
{
    /// <summary>
    /// Starts a new pipeline execution.
    /// </summary>
    Task<ExecutionResponse> StartExecutionAsync(Guid pipelineId, string triggeredBy, Guid? userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets an execution by ID.
    /// </summary>
    Task<ExecutionResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all executions with filtering and pagination.
    /// </summary>
    Task<PagedExecutionResponse> GetAllAsync(ExecutionSearchRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels a running execution.
    /// </summary>
    Task<ExecutionResponse> CancelExecutionAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets execution statistics.
    /// </summary>
    Task<ExecutionStatsDto> GetStatsAsync(Guid? pipelineId = null, CancellationToken cancellationToken = default);
}
