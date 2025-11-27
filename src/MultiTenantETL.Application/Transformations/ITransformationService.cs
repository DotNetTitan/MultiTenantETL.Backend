using MultiTenantETL.Application.Transformations.Models;

namespace MultiTenantETL.Application.Transformations;

public interface ITransformationService
{
    Task<TransformationResponse> CreateAsync(CreateTransformationRequest request, CancellationToken cancellationToken = default);
    Task<TransformationResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedTransformationResponse> GetAllAsync(TransformationSearchRequest request, CancellationToken cancellationToken = default);
    Task<TransformationResponse> UpdateAsync(Guid id, UpdateTransformationRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
