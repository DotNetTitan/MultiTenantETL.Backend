using MultiTenantETL.Application.Executions.Models;

namespace MultiTenantETL.Application.Executions;

public interface IExecutionService
{
    Task<ExecutionResponse> StartExecutionAsync(Guid pipelineId, string triggeredBy, Guid? userId, CancellationToken cancellationToken = default);
    Task<ExecutionResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedExecutionResponse> GetAllAsync(ExecutionSearchRequest request, CancellationToken cancellationToken = default);
    Task<ExecutionResponse> CancelExecutionAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ExecutionStatsDto> GetStatsAsync(Guid? pipelineId = null, CancellationToken cancellationToken = default);
}
