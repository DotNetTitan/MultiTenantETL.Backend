using MultiTenantETL.Application.Pipelines.Models;

namespace MultiTenantETL.Application.Pipelines;

/// <summary>
/// Service for managing pipelines.
/// </summary>
public interface IPipelineService
{
    /// <summary>
    /// Creates a new pipeline.
    /// </summary>
    Task<PipelineResponse> CreateAsync(CreatePipelineRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a pipeline by ID.
    /// </summary>
    Task<PipelineResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all pipelines with pagination.
    /// </summary>
    Task<PagedPipelineResponse> GetAllAsync(PipelineSearchRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing pipeline.
    /// </summary>
    Task<PipelineResponse> UpdateAsync(Guid id, UpdatePipelineRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a pipeline.
    /// </summary>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Toggles the active status of a pipeline.
    /// </summary>
    Task<PipelineResponse> ToggleStatusAsync(Guid id, CancellationToken cancellationToken = default);
}
