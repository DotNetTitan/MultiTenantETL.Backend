using MultiTenantETL.Application.Pipelines.Models;

namespace MultiTenantETL.Application.Pipelines;

public interface IPipelineService
{
    Task<PipelineResponse> CreateAsync(CreatePipelineRequest request, CancellationToken cancellationToken = default);
    Task<PipelineResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedPipelineResponse> GetAllAsync(PipelineSearchRequest request, CancellationToken cancellationToken = default);
    Task<PipelineResponse> UpdateAsync(Guid id, UpdatePipelineRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PipelineResponse> ToggleStatusAsync(Guid id, CancellationToken cancellationToken = default);
}
