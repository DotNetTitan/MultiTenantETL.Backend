using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MultiTenantETL.Application.Common.Interfaces;
using MultiTenantETL.Application.Interfaces;
using MultiTenantETL.Application.Pipelines;
using MultiTenantETL.Application.Pipelines.Models;
using MultiTenantETL.Application.Scheduling;
using MultiTenantETL.Domain.Constants;
using MultiTenantETL.Domain.Entities;
using MultiTenantETL.Infrastructure.Configuration;
using MultiTenantETL.Infrastructure.Persistence;

namespace MultiTenantETL.Infrastructure.Services;

public class PipelineService : IPipelineService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<PipelineService> _logger;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAuditService _auditService;
    private readonly IScheduleService _scheduleService;

    public PipelineService(
        ApplicationDbContext context,
        ILogger<PipelineService> logger,
        ICurrentUserService currentUserService,
        IAuditService auditService,
        IScheduleService scheduleService)
    {
        _context = context;
        _logger = logger;
        _currentUserService = currentUserService;
        _auditService = auditService;
        _scheduleService = scheduleService;
    }

    public async Task<PipelineResponse> CreateAsync(CreatePipelineRequest request, CancellationToken cancellationToken = default)
    {
        var tenantId = _currentUserService.GetTenantId();
        var userId = _currentUserService.GetUserId();

        _logger.LogInformation("Creating pipeline {Name} for tenant {TenantId}", request.Name, tenantId);

        // Validate connectors exist and belong to tenant
        var sourceConnector = await _context.Connectors
            .Where(c => c.Id == request.SourceConnectorId && c.TenantId == tenantId)
            .FirstOrDefaultAsync(cancellationToken);

        if (sourceConnector == null)
        {
            throw new KeyNotFoundException($"Source connector with ID {request.SourceConnectorId} not found");
        }

        var destinationConnector = await _context.Connectors
            .Where(c => c.Id == request.DestinationConnectorId && c.TenantId == tenantId)
            .FirstOrDefaultAsync(cancellationToken);

        if (destinationConnector == null)
        {
            throw new KeyNotFoundException($"Destination connector with ID {request.DestinationConnectorId} not found");
        }

        var pipeline = new Pipeline
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = request.Name,
            Description = request.Description,
            SourceConnectorId = request.SourceConnectorId,
            DestinationConnectorId = request.DestinationConnectorId,
            Status = "Idle",
            FieldMappingsJson = NormalizeFieldMappings(request.FieldMappings),
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = userId
        };

        _context.Pipelines.Add(pipeline);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Pipeline {PipelineId} created successfully", pipeline.Id);

        await _auditService.LogAsync(
            action: AuditActions.Pipelines.Created,
            resourceType: "Pipeline",
            resourceId: pipeline.Id.ToString(),
            description: $"Created pipeline '{pipeline.Name}'",
            metadata: new { pipeline.SourceConnectorId, pipeline.DestinationConnectorId }
        );

        return await MapToResponseAsync(pipeline, cancellationToken);
    }

    public async Task<PipelineResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tenantId = _currentUserService.GetTenantId();

        var pipeline = await _context.Pipelines
            .Include(p => p.SourceConnector)
            .Include(p => p.DestinationConnector)
            .Include(p => p.Schedule)
            .Where(p => p.Id == id && p.TenantId == tenantId)
            .FirstOrDefaultAsync(cancellationToken);

        if (pipeline == null)
        {
            throw new KeyNotFoundException($"Pipeline with ID {id} not found");
        }

        return await MapToResponseAsync(pipeline, cancellationToken);
    }

    public async Task<PagedPipelineResponse> GetAllAsync(PipelineSearchRequest request, CancellationToken cancellationToken = default)
    {
        var tenantId = _currentUserService.GetTenantId();

        var query = _context.Pipelines
            .Include(p => p.SourceConnector)
            .Include(p => p.DestinationConnector)
            .Include(p => p.Schedule)
            .Where(p => p.TenantId == tenantId);

        // Apply search filter
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            query = query.Where(p => p.Name.Contains(request.Search) ||
                                    (p.Description != null && p.Description.Contains(request.Search)));
        }

        // Apply name filter
        if (!string.IsNullOrWhiteSpace(request.Name))
        {
            query = query.Where(p => p.Name.Contains(request.Name));
        }

        // Apply status filter
        if (!string.IsNullOrWhiteSpace(request.Status) && request.Status != "All")
        {
            query = query.Where(p => p.Status == request.Status);
        }

        // Apply scheduled filter - IsScheduled is derived from having an active Schedule
        if (request.IsScheduled.HasValue)
        {
            if (request.IsScheduled.Value)
            {
                query = query.Where(p => p.Schedule != null && p.Schedule.IsActive);
            }
            else
            {
                query = query.Where(p => p.Schedule == null || !p.Schedule.IsActive);
            }
        }

        // Apply active filter
        if (request.IsActive.HasValue)
        {
            query = query.Where(p => p.IsActive == request.IsActive.Value);
        }

        // Apply sorting
        query = ApplySorting(query, request.SortBy);

        var totalCount = await query.CountAsync(cancellationToken);
        var totalPages = (int)Math.Ceiling(totalCount / (double)request.PageSize);

        var pipelines = await query
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedPipelineResponse
        {
            Pipelines = pipelines.Select(MapToListResponse).ToList(),
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize,
            TotalPages = totalPages
        };
    }

    public async Task<PipelineResponse> UpdateAsync(Guid id, UpdatePipelineRequest request, CancellationToken cancellationToken = default)
    {
        var tenantId = _currentUserService.GetTenantId();
        var userId = _currentUserService.GetUserId();

        var pipeline = await _context.Pipelines
            .Where(p => p.Id == id && p.TenantId == tenantId)
            .FirstOrDefaultAsync(cancellationToken);

        if (pipeline == null)
        {
            throw new KeyNotFoundException($"Pipeline with ID {id} not found");
        }

        _logger.LogInformation("Updating pipeline {PipelineId}", id);

        var oldName = pipeline.Name;
        var oldIsActive = pipeline.IsActive;

        pipeline.Name = request.Name;
        pipeline.Description = request.Description;
        pipeline.FieldMappingsJson = NormalizeFieldMappings(request.FieldMappings);

        if (request.IsActive.HasValue)
        {
            pipeline.IsActive = request.IsActive.Value;
        }

        pipeline.UpdatedAt = DateTime.UtcNow;
        pipeline.UpdatedBy = userId;

        await _context.SaveChangesAsync(cancellationToken);

        // Handle schedule pause/resume when IsActive changes
        if (oldIsActive != pipeline.IsActive)
        {
            if (!pipeline.IsActive)
            {
                // Pipeline was deactivated - pause schedules
                await _scheduleService.PauseSchedulesForPipelineAsync(id, cancellationToken);
            }
            else
            {
                // Pipeline was activated - resume schedules
                await _scheduleService.ResumeSchedulesForPipelineAsync(id, cancellationToken);
            }
        }

        _logger.LogInformation("Pipeline {PipelineId} updated successfully", id);

        var changes = new List<string>();
        if (oldName != pipeline.Name) changes.Add($"name: '{oldName}' → '{pipeline.Name}'");
        if (oldIsActive != pipeline.IsActive) changes.Add($"active: {oldIsActive} → {pipeline.IsActive}");

        await _auditService.LogAsync(
            action: AuditActions.Pipelines.Updated,
            resourceType: "Pipeline",
            resourceId: pipeline.Id.ToString(),
            description: $"Updated pipeline '{pipeline.Name}'",
            metadata: new { Changes = changes }
        );

        return await MapToResponseAsync(pipeline, cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tenantId = _currentUserService.GetTenantId();

        var pipeline = await _context.Pipelines
            .Where(p => p.Id == id && p.TenantId == tenantId)
            .FirstOrDefaultAsync(cancellationToken);

        if (pipeline == null)
        {
            throw new KeyNotFoundException($"Pipeline with ID {id} not found");
        }

        _logger.LogInformation("Deleting pipeline {PipelineId}", id);

        var pipelineName = pipeline.Name;

        _context.Pipelines.Remove(pipeline);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Pipeline {PipelineId} deleted successfully", id);

        await _auditService.LogAsync(
            action: AuditActions.Pipelines.Deleted,
            resourceType: "Pipeline",
            resourceId: id.ToString(),
            description: $"Deleted pipeline '{pipelineName}'",
            metadata: new { Name = pipelineName }
        );
    }

    public async Task<PipelineResponse> ToggleStatusAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tenantId = _currentUserService.GetTenantId();
        var userId = _currentUserService.GetUserId();

        var pipeline = await _context.Pipelines
            .Where(p => p.Id == id && p.TenantId == tenantId)
            .FirstOrDefaultAsync(cancellationToken);

        if (pipeline == null)
        {
            throw new KeyNotFoundException($"Pipeline with ID {id} not found");
        }

        var wasActive = pipeline.IsActive;
        pipeline.IsActive = !pipeline.IsActive;
        pipeline.UpdatedAt = DateTime.UtcNow;
        pipeline.UpdatedBy = userId;

        await _context.SaveChangesAsync(cancellationToken);

        // Handle schedule pause/resume
        if (wasActive && !pipeline.IsActive)
        {
            // Pipeline was deactivated - pause schedules
            await _scheduleService.PauseSchedulesForPipelineAsync(id, cancellationToken);
        }
        else if (!wasActive && pipeline.IsActive)
        {
            // Pipeline was activated - resume schedules
            await _scheduleService.ResumeSchedulesForPipelineAsync(id, cancellationToken);
        }

        var action = pipeline.IsActive ? AuditActions.Pipelines.Activated : AuditActions.Pipelines.Deactivated;
        await _auditService.LogAsync(
            action: action,
            resourceType: "Pipeline",
            resourceId: pipeline.Id.ToString(),
            description: $"{(pipeline.IsActive ? "Activated" : "Deactivated")} pipeline '{pipeline.Name}'",
            metadata: new { pipeline.IsActive }
        );

        return await MapToResponseAsync(pipeline, cancellationToken);
    }

    private static IQueryable<Pipeline> ApplySorting(IQueryable<Pipeline> query, string? sortBy)
    {
        return sortBy switch
        {
            "name_asc" => query.OrderBy(p => p.Name),
            "name_desc" => query.OrderByDescending(p => p.Name),
            "created_desc" => query.OrderByDescending(p => p.CreatedAt),
            "created_asc" => query.OrderBy(p => p.CreatedAt),
            "lastRun_desc" => query.OrderByDescending(p => p.LastRunAt ?? DateTime.MinValue),
            "lastRun_asc" => query.OrderBy(p => p.LastRunAt ?? DateTime.MinValue),
            _ => query.OrderByDescending(p => p.CreatedAt)
        };
    }

    private async Task<PipelineResponse> MapToResponseAsync(Pipeline pipeline, CancellationToken cancellationToken)
    {
        // Load connectors if not already loaded
        if (pipeline.SourceConnector == null || pipeline.DestinationConnector == null)
        {
            await _context.Entry(pipeline)
                .Reference(p => p.SourceConnector)
                .LoadAsync(cancellationToken);

            await _context.Entry(pipeline)
                .Reference(p => p.DestinationConnector)
                .LoadAsync(cancellationToken);
        }

        // Load schedule if not already loaded
        if (!_context.Entry(pipeline).Reference(p => p.Schedule).IsLoaded)
        {
            await _context.Entry(pipeline)
                .Reference(p => p.Schedule)
                .LoadAsync(cancellationToken);
        }

        return new PipelineResponse
        {
            Id = pipeline.Id,
            TenantId = pipeline.TenantId,
            Name = pipeline.Name,
            Description = pipeline.Description,
            SourceConnectorId = pipeline.SourceConnectorId,
            SourceConnectorName = pipeline.SourceConnector?.Name,
            DestinationConnectorId = pipeline.DestinationConnectorId,
            DestinationConnectorName = pipeline.DestinationConnector?.Name,
            Status = pipeline.Status,
            FieldMappings = JsonSerializer.Deserialize<JsonElement>(pipeline.FieldMappingsJson),
            IsScheduled = pipeline.Schedule?.IsActive ?? false,
            IsActive = pipeline.IsActive,
            LastRunAt = pipeline.LastRunAt,
            LastRunStatus = pipeline.LastRunStatus,
            LastRunRecordsProcessed = pipeline.LastRunRecordsProcessed,
            CreatedAt = pipeline.CreatedAt,
            UpdatedAt = pipeline.UpdatedAt
        };
    }

    private static PipelineListResponse MapToListResponse(Pipeline pipeline)
    {
        return new PipelineListResponse
        {
            Id = pipeline.Id,
            Name = pipeline.Name,
            Description = pipeline.Description,
            SourceConnectorName = pipeline.SourceConnector?.Name,
            DestinationConnectorName = pipeline.DestinationConnector?.Name,
            Status = pipeline.Status,
            IsScheduled = pipeline.Schedule?.IsActive ?? false,
            IsActive = pipeline.IsActive,
            LastRunAt = pipeline.LastRunAt,
            LastRunStatus = pipeline.LastRunStatus,
            CreatedAt = pipeline.CreatedAt
        };
    }

    /// <summary>
    /// Normalizes field mappings from frontend by regenerating proper server-side IDs.
    /// Frontend generates temporary IDs (e.g., "trans-1701629000000-0.123") which should be
    /// replaced with proper GUIDs when saving to the database.
    /// </summary>
    /// <param name="fieldMappingsElement">The field mappings JSON element from the frontend request.</param>
    /// <returns>A JSON string with field mappings containing proper server-generated GUID IDs.</returns>
    /// <exception cref="ArgumentException">Thrown when the field mappings JSON cannot be parsed.</exception>
    private static string NormalizeFieldMappings(JsonElement fieldMappingsElement)
    {
        // Handle empty or null mappings
        if (fieldMappingsElement.ValueKind == JsonValueKind.Null ||
            (fieldMappingsElement.ValueKind == JsonValueKind.Array && fieldMappingsElement.GetArrayLength() == 0))
        {
            return "[]";
        }

        try
        {
            var mappings = JsonSerializer.Deserialize<List<FieldMappingInternal>>(fieldMappingsElement.GetRawText(), JsonSerializerOptionsProvider.Default);
            if (mappings == null || mappings.Count == 0)
            {
                return "[]";
            }

            // Regenerate IDs for each mapping and its transformations
            var normalizedMappings = mappings.Select((mapping, index) => new FieldMappingInternal
            {
                Id = Guid.NewGuid().ToString(), // Generate proper GUID for field mapping
                SourceFields = mapping.SourceFields ?? new List<string>(),
                DestinationField = mapping.DestinationField ?? string.Empty,
                Order = mapping.Order > 0 ? mapping.Order : index + 1,
                Transformations = NormalizeTransformations(mapping.Transformations)
            }).ToList();

            return JsonSerializer.Serialize(normalizedMappings, JsonSerializerOptionsProvider.Default);
        }
        catch (JsonException ex)
        {
            throw new ArgumentException($"Invalid field mappings format: {ex.Message}", nameof(fieldMappingsElement), ex);
        }
    }

    /// <summary>
    /// Normalizes transformations by regenerating proper server-side IDs.
    /// </summary>
    /// <param name="transformations">The list of transformations to normalize, or null.</param>
    /// <returns>A new list of transformations with proper server-generated GUID IDs.</returns>
    private static List<FieldTransformationInternal> NormalizeTransformations(List<FieldTransformationInternal>? transformations)
    {
        if (transformations == null || transformations.Count == 0)
        {
            return new List<FieldTransformationInternal>();
        }

        return transformations.Select((trans, index) => new FieldTransformationInternal
        {
            Id = Guid.NewGuid().ToString(), // Generate proper GUID for transformation
            Type = trans.Type ?? string.Empty,
            Config = trans.Config,
            Order = trans.Order > 0 ? trans.Order : index + 1,
            IsEnabled = trans.IsEnabled
        }).ToList();
    }

    // Internal DTOs for field mapping normalization (allow mutable properties)
    private class FieldMappingInternal
    {
        public string Id { get; set; } = string.Empty;
        public List<string> SourceFields { get; set; } = new();
        public string DestinationField { get; set; } = string.Empty;
        public List<FieldTransformationInternal> Transformations { get; set; } = new();
        public int Order { get; set; }
    }

    private class FieldTransformationInternal
    {
        public string Id { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public JsonElement? Config { get; set; }
        public int Order { get; set; }
        public bool IsEnabled { get; set; } = true;
    }
}
