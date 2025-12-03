using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MultiTenantETL.Application.Common.Interfaces;
using MultiTenantETL.Application.Interfaces;
using MultiTenantETL.Application.Pipelines;
using MultiTenantETL.Application.Pipelines.Models;
using MultiTenantETL.Domain.Constants;
using MultiTenantETL.Domain.Entities;
using MultiTenantETL.Infrastructure.Persistence;

namespace MultiTenantETL.Infrastructure.Services;

public class PipelineService : IPipelineService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<PipelineService> _logger;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAuditService _auditService;

    public PipelineService(
        ApplicationDbContext context,
        ILogger<PipelineService> logger,
        ICurrentUserService currentUserService,
        IAuditService auditService)
    {
        _context = context;
        _logger = logger;
        _currentUserService = currentUserService;
        _auditService = auditService;
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
            ScheduleJson = request.Schedule.HasValue ? JsonSerializer.Serialize(request.Schedule.Value) : null,
            IsScheduled = request.IsScheduled,
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
            metadata: new { pipeline.SourceConnectorId, pipeline.DestinationConnectorId, pipeline.IsScheduled }
        );

        return await MapToResponseAsync(pipeline, cancellationToken);
    }

    public async Task<PipelineResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tenantId = _currentUserService.GetTenantId();

        var pipeline = await _context.Pipelines
            .Include(p => p.SourceConnector)
            .Include(p => p.DestinationConnector)
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

        // Apply scheduled filter
        if (request.IsScheduled.HasValue)
        {
            query = query.Where(p => p.IsScheduled == request.IsScheduled.Value);
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
        pipeline.ScheduleJson = request.Schedule.HasValue ? JsonSerializer.Serialize(request.Schedule.Value) : null;
        pipeline.IsScheduled = request.IsScheduled;

        if (request.IsActive.HasValue)
        {
            pipeline.IsActive = request.IsActive.Value;
        }

        pipeline.UpdatedAt = DateTime.UtcNow;
        pipeline.UpdatedBy = userId;

        await _context.SaveChangesAsync(cancellationToken);

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

        pipeline.IsActive = !pipeline.IsActive;
        pipeline.UpdatedAt = DateTime.UtcNow;
        pipeline.UpdatedBy = userId;

        await _context.SaveChangesAsync(cancellationToken);

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
            Schedule = !string.IsNullOrEmpty(pipeline.ScheduleJson)
                ? JsonSerializer.Deserialize<JsonElement>(pipeline.ScheduleJson)
                : null,
            IsScheduled = pipeline.IsScheduled,
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
            IsScheduled = pipeline.IsScheduled,
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
    private static string NormalizeFieldMappings(JsonElement fieldMappingsElement)
    {
        var options = new JsonSerializerOptions 
        { 
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        // Handle empty or null mappings
        if (fieldMappingsElement.ValueKind == JsonValueKind.Null ||
            (fieldMappingsElement.ValueKind == JsonValueKind.Array && fieldMappingsElement.GetArrayLength() == 0))
        {
            return "[]";
        }

        try
        {
            var mappings = JsonSerializer.Deserialize<List<FieldMappingInternal>>(fieldMappingsElement.GetRawText(), options);
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

            return JsonSerializer.Serialize(normalizedMappings, options);
        }
        catch (JsonException)
        {
            // If parsing fails, return the original JSON as-is
            return fieldMappingsElement.GetRawText();
        }
    }

    /// <summary>
    /// Normalizes transformations by regenerating proper server-side IDs.
    /// </summary>
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
