using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MultiTenantETL.Application.Common.Interfaces;
using MultiTenantETL.Application.Interfaces;
using MultiTenantETL.Application.Pipelines.Commands;
using MultiTenantETL.Application.Pipelines.Models;
using MultiTenantETL.Application.Pipelines.Queries;
using MultiTenantETL.Application.Executions;
using MultiTenantETL.Application.Executions.Models;
using MultiTenantETL.Domain.Constants;
using MultiTenantETL.Domain.Entities;
using MultiTenantETL.Infrastructure.Configuration;
using MultiTenantETL.Infrastructure.Persistence;

namespace MultiTenantETL.Infrastructure.Handlers;

/// <summary>
/// Wolverine handlers for Pipeline commands and queries
/// </summary>
public class PipelineHandler
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<PipelineHandler> _logger;
    private readonly IAuditService _auditService;
    private readonly IExecutionService _executionService;

    public PipelineHandler(
        ApplicationDbContext context,
        ILogger<PipelineHandler> logger,
        IAuditService auditService,
        IExecutionService executionService)
    {
        _context = context;
        _logger = logger;
        _auditService = auditService;
        _executionService = executionService;
    }

    /// <summary>
    /// Handle CreatePipelineCommand
    /// </summary>
    public async Task<PipelineResponse> Handle(CreatePipelineCommand command)
    {
        _logger.LogInformation("Creating pipeline {Name} for tenant {TenantId}", command.Name, command.TenantId);

        // Validate connectors exist and belong to tenant
        var sourceConnector = await _context.Connectors
            .Where(c => c.Id == command.SourceConnectorId && c.TenantId == command.TenantId)
            .FirstOrDefaultAsync();

        if (sourceConnector == null)
        {
            throw new KeyNotFoundException($"Source connector with ID {command.SourceConnectorId} not found");
        }

        var destinationConnector = await _context.Connectors
            .Where(c => c.Id == command.DestinationConnectorId && c.TenantId == command.TenantId)
            .FirstOrDefaultAsync();

        if (destinationConnector == null)
        {
            throw new KeyNotFoundException($"Destination connector with ID {command.DestinationConnectorId} not found");
        }

        var pipeline = new Pipeline
        {
            Id = Guid.NewGuid(),
            TenantId = command.TenantId,
            Name = command.Name,
            Description = command.Description,
            SourceConnectorId = command.SourceConnectorId,
            DestinationConnectorId = command.DestinationConnectorId,
            Status = "Idle",
            FieldMappingsJson = NormalizeFieldMappings(command.FieldMappings),
            ScheduleJson = command.Schedule.HasValue ? JsonSerializer.Serialize(command.Schedule.Value) : null,
            IsScheduled = command.IsScheduled,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = command.UserId
        };

        _context.Pipelines.Add(pipeline);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Pipeline {PipelineId} created successfully", pipeline.Id);

        await _auditService.LogAsync(
            action: AuditActions.Pipelines.Created,
            resourceType: "Pipeline",
            resourceId: pipeline.Id.ToString(),
            description: $"Created pipeline '{pipeline.Name}'",
            metadata: new { pipeline.SourceConnectorId, pipeline.DestinationConnectorId, pipeline.IsScheduled }
        );

        return await MapToResponseAsync(pipeline, sourceConnector, destinationConnector);
    }

    /// <summary>
    /// Handle UpdatePipelineCommand
    /// </summary>
    public async Task<PipelineResponse> Handle(UpdatePipelineCommand command)
    {
        var pipeline = await _context.Pipelines
            .Where(p => p.Id == command.Id && p.TenantId == command.TenantId)
            .FirstOrDefaultAsync();

        if (pipeline == null)
        {
            throw new KeyNotFoundException($"Pipeline with ID {command.Id} not found");
        }

        _logger.LogInformation("Updating pipeline {PipelineId}", command.Id);

        var oldName = pipeline.Name;
        var oldIsActive = pipeline.IsActive;

        pipeline.Name = command.Name;
        pipeline.Description = command.Description;
        pipeline.FieldMappingsJson = NormalizeFieldMappings(command.FieldMappings);
        pipeline.ScheduleJson = command.Schedule.HasValue ? JsonSerializer.Serialize(command.Schedule.Value) : null;
        pipeline.IsScheduled = command.IsScheduled;

        if (command.IsActive.HasValue)
        {
            pipeline.IsActive = command.IsActive.Value;
        }

        pipeline.UpdatedAt = DateTime.UtcNow;
        pipeline.UpdatedBy = command.UserId;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Pipeline {PipelineId} updated successfully", command.Id);

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

        return await MapToResponseAsync(pipeline);
    }

    /// <summary>
    /// Handle DeletePipelineCommand
    /// </summary>
    public async Task Handle(DeletePipelineCommand command)
    {
        var pipeline = await _context.Pipelines
            .Where(p => p.Id == command.Id && p.TenantId == command.TenantId)
            .FirstOrDefaultAsync();

        if (pipeline == null)
        {
            throw new KeyNotFoundException($"Pipeline with ID {command.Id} not found");
        }

        _logger.LogInformation("Deleting pipeline {PipelineId}", command.Id);

        var pipelineName = pipeline.Name;

        _context.Pipelines.Remove(pipeline);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Pipeline {PipelineId} deleted successfully", command.Id);

        await _auditService.LogAsync(
            action: AuditActions.Pipelines.Deleted,
            resourceType: "Pipeline",
            resourceId: command.Id.ToString(),
            description: $"Deleted pipeline '{pipelineName}'",
            metadata: new { Name = pipelineName }
        );
    }

    /// <summary>
    /// Handle TogglePipelineStatusCommand
    /// </summary>
    public async Task<PipelineResponse> Handle(TogglePipelineStatusCommand command)
    {
        var pipeline = await _context.Pipelines
            .Where(p => p.Id == command.Id && p.TenantId == command.TenantId)
            .FirstOrDefaultAsync();

        if (pipeline == null)
        {
            throw new KeyNotFoundException($"Pipeline with ID {command.Id} not found");
        }

        pipeline.IsActive = !pipeline.IsActive;
        pipeline.UpdatedAt = DateTime.UtcNow;
        pipeline.UpdatedBy = command.UserId;

        await _context.SaveChangesAsync();

        var action = pipeline.IsActive ? AuditActions.Pipelines.Activated : AuditActions.Pipelines.Deactivated;
        await _auditService.LogAsync(
            action: action,
            resourceType: "Pipeline",
            resourceId: pipeline.Id.ToString(),
            description: $"{(pipeline.IsActive ? "Activated" : "Deactivated")} pipeline '{pipeline.Name}'",
            metadata: new { pipeline.IsActive }
        );

        return await MapToResponseAsync(pipeline);
    }

    /// <summary>
    /// Handle ExecutePipelineCommand
    /// </summary>
    public async Task<ExecutionResponse> Handle(ExecutePipelineCommand command)
    {
        var pipeline = await _context.Pipelines
            .Where(p => p.Id == command.Id && p.TenantId == command.TenantId)
            .FirstOrDefaultAsync();

        if (pipeline == null)
        {
            throw new KeyNotFoundException($"Pipeline with ID {command.Id} not found");
        }

        return await _executionService.StartExecutionAsync(command.Id, "Manual", command.UserId);
    }

    /// <summary>
    /// Handle GetPipelineByIdQuery
    /// </summary>
    public async Task<PipelineResponse> Handle(GetPipelineByIdQuery query)
    {
        var pipeline = await _context.Pipelines
            .Include(p => p.SourceConnector)
            .Include(p => p.DestinationConnector)
            .Where(p => p.Id == query.Id && p.TenantId == query.TenantId)
            .FirstOrDefaultAsync();

        if (pipeline == null)
        {
            throw new KeyNotFoundException($"Pipeline with ID {query.Id} not found");
        }

        return await MapToResponseAsync(pipeline);
    }

    /// <summary>
    /// Handle SearchPipelinesQuery
    /// </summary>
    public async Task<PagedPipelineResponse> Handle(SearchPipelinesQuery query)
    {
        var dbQuery = _context.Pipelines
            .Include(p => p.SourceConnector)
            .Include(p => p.DestinationConnector)
            .Where(p => p.TenantId == query.TenantId);

        // Apply search filter
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            dbQuery = dbQuery.Where(p => p.Name.Contains(query.Search) ||
                                        (p.Description != null && p.Description.Contains(query.Search)));
        }

        // Apply name filter
        if (!string.IsNullOrWhiteSpace(query.Name))
        {
            dbQuery = dbQuery.Where(p => p.Name.Contains(query.Name));
        }

        // Apply status filter
        if (!string.IsNullOrWhiteSpace(query.Status) && query.Status != "All")
        {
            dbQuery = dbQuery.Where(p => p.Status == query.Status);
        }

        // Apply scheduled filter
        if (query.IsScheduled.HasValue)
        {
            dbQuery = dbQuery.Where(p => p.IsScheduled == query.IsScheduled.Value);
        }

        // Apply active filter
        if (query.IsActive.HasValue)
        {
            dbQuery = dbQuery.Where(p => p.IsActive == query.IsActive.Value);
        }

        // Apply sorting
        dbQuery = ApplySorting(dbQuery, query.SortBy);

        var totalCount = await dbQuery.CountAsync();
        var totalPages = (int)Math.Ceiling(totalCount / (double)query.PageSize);

        var pipelines = await dbQuery
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync();

        return new PagedPipelineResponse
        {
            Pipelines = pipelines.Select(MapToListResponse).ToList(),
            TotalCount = totalCount,
            Page = query.Page,
            PageSize = query.PageSize,
            TotalPages = totalPages
        };
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

    private async Task<PipelineResponse> MapToResponseAsync(Pipeline pipeline, Connector? sourceConnector = null, Connector? destinationConnector = null)
    {
        // Load connectors if not already loaded
        if (pipeline.SourceConnector == null && sourceConnector == null)
        {
            await _context.Entry(pipeline)
                .Reference(p => p.SourceConnector)
                .LoadAsync();
        }

        if (pipeline.DestinationConnector == null && destinationConnector == null)
        {
            await _context.Entry(pipeline)
                .Reference(p => p.DestinationConnector)
                .LoadAsync();
        }

        return new PipelineResponse
        {
            Id = pipeline.Id,
            TenantId = pipeline.TenantId,
            Name = pipeline.Name,
            Description = pipeline.Description,
            SourceConnectorId = pipeline.SourceConnectorId,
            SourceConnectorName = sourceConnector?.Name ?? pipeline.SourceConnector?.Name,
            DestinationConnectorId = pipeline.DestinationConnectorId,
            DestinationConnectorName = destinationConnector?.Name ?? pipeline.DestinationConnector?.Name,
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

    private static string NormalizeFieldMappings(JsonElement fieldMappingsElement)
    {
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

            var normalizedMappings = mappings.Select((mapping, index) => new FieldMappingInternal
            {
                Id = Guid.NewGuid().ToString(),
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

    private static List<FieldTransformationInternal> NormalizeTransformations(List<FieldTransformationInternal>? transformations)
    {
        if (transformations == null || transformations.Count == 0)
        {
            return new List<FieldTransformationInternal>();
        }

        return transformations.Select((trans, index) => new FieldTransformationInternal
        {
            Id = Guid.NewGuid().ToString(),
            Type = trans.Type ?? string.Empty,
            Config = trans.Config,
            Order = trans.Order > 0 ? trans.Order : index + 1,
            IsEnabled = trans.IsEnabled
        }).ToList();
    }

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
