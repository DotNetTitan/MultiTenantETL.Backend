using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MultiTenantETL.Application.Common.Interfaces;
using MultiTenantETL.Application.Interfaces;
using MultiTenantETL.Application.Transformations.Commands;
using MultiTenantETL.Application.Transformations.Models;
using MultiTenantETL.Application.Transformations.Queries;
using MultiTenantETL.Domain.Constants;
using MultiTenantETL.Domain.Entities;
using MultiTenantETL.Infrastructure.Persistence;

namespace MultiTenantETL.Infrastructure.Handlers;

/// <summary>
/// Wolverine handlers for Transformation commands and queries
/// </summary>
public class TransformationHandler
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<TransformationHandler> _logger;
    private readonly IAuditService _auditService;

    public TransformationHandler(
        ApplicationDbContext context,
        ILogger<TransformationHandler> logger,
        IAuditService auditService)
    {
        _context = context;
        _logger = logger;
        _auditService = auditService;
    }

    /// <summary>
    /// Handle CreateTransformationCommand
    /// </summary>
    public async Task<TransformationResponse> Handle(CreateTransformationCommand command)
    {
        _logger.LogInformation("Creating transformation {Name} for tenant {TenantId}", command.Name, command.TenantId);

        var transformation = new Transformation
        {
            Id = Guid.NewGuid(),
            TenantId = command.TenantId,
            Name = command.Name,
            Description = command.Description,
            Type = command.Type,
            ConfigJson = JsonSerializer.Serialize(command.Config),
            CreatedAt = DateTime.UtcNow,
            CreatedBy = command.UserId
        };

        _context.Transformations.Add(transformation);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Transformation {TransformationId} created successfully", transformation.Id);

        await _auditService.LogAsync(
            action: AuditActions.TransformationCreated,
            resourceType: "Transformation",
            resourceId: transformation.Id.ToString(),
            description: $"Created transformation '{transformation.Name}' ({transformation.Type})",
            metadata: new { transformation.Type }
        );

        return MapToResponse(transformation);
    }

    /// <summary>
    /// Handle UpdateTransformationCommand
    /// </summary>
    public async Task<TransformationResponse> Handle(UpdateTransformationCommand command)
    {
        var transformation = await _context.Transformations
            .Where(t => t.Id == command.Id && t.TenantId == command.TenantId)
            .FirstOrDefaultAsync();

        if (transformation == null)
        {
            throw new KeyNotFoundException($"Transformation with ID {command.Id} not found");
        }

        _logger.LogInformation("Updating transformation {TransformationId}", command.Id);

        var oldName = transformation.Name;

        transformation.Name = command.Name;
        transformation.Description = command.Description;
        transformation.ConfigJson = JsonSerializer.Serialize(command.Config);
        transformation.UpdatedAt = DateTime.UtcNow;
        transformation.UpdatedBy = command.UserId;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Transformation {TransformationId} updated successfully", command.Id);

        var changes = new List<string>();
        if (oldName != transformation.Name) changes.Add($"name: '{oldName}' → '{transformation.Name}'");

        await _auditService.LogAsync(
            action: AuditActions.TransformationUpdated,
            resourceType: "Transformation",
            resourceId: transformation.Id.ToString(),
            description: $"Updated transformation '{transformation.Name}'",
            metadata: new { Changes = changes }
        );

        return MapToResponse(transformation);
    }

    /// <summary>
    /// Handle DeleteTransformationCommand
    /// </summary>
    public async Task Handle(DeleteTransformationCommand command)
    {
        var transformation = await _context.Transformations
            .Where(t => t.Id == command.Id && t.TenantId == command.TenantId)
            .FirstOrDefaultAsync();

        if (transformation == null)
        {
            throw new KeyNotFoundException($"Transformation with ID {command.Id} not found");
        }

        _logger.LogInformation("Deleting transformation {TransformationId}", command.Id);

        var transformationName = transformation.Name;
        var transformationType = transformation.Type;

        _context.Transformations.Remove(transformation);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Transformation {TransformationId} deleted successfully", command.Id);

        await _auditService.LogAsync(
            action: AuditActions.TransformationDeleted,
            resourceType: "Transformation",
            resourceId: command.Id.ToString(),
            description: $"Deleted transformation '{transformationName}' ({transformationType})",
            metadata: new { Type = transformationType }
        );
    }

    /// <summary>
    /// Handle GetTransformationByIdQuery
    /// </summary>
    public async Task<TransformationResponse> Handle(GetTransformationByIdQuery query)
    {
        var transformation = await _context.Transformations
            .Where(t => t.Id == query.Id && t.TenantId == query.TenantId)
            .FirstOrDefaultAsync();

        if (transformation == null)
        {
            throw new KeyNotFoundException($"Transformation with ID {query.Id} not found");
        }

        return MapToResponse(transformation);
    }

    /// <summary>
    /// Handle SearchTransformationsQuery
    /// </summary>
    public async Task<PagedTransformationResponse> Handle(SearchTransformationsQuery query)
    {
        var dbQuery = _context.Transformations
            .Where(t => t.TenantId == query.TenantId);

        // Apply search filter
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            dbQuery = dbQuery.Where(t => t.Name.Contains(query.Search) ||
                                        (t.Description != null && t.Description.Contains(query.Search)));
        }

        // Apply name filter
        if (!string.IsNullOrWhiteSpace(query.Name))
        {
            dbQuery = dbQuery.Where(t => t.Name.Contains(query.Name));
        }

        // Apply type filter
        if (!string.IsNullOrWhiteSpace(query.Type) && query.Type != "All")
        {
            dbQuery = dbQuery.Where(t => t.Type == query.Type);
        }

        // Apply sorting
        dbQuery = ApplySorting(dbQuery, query.Sort);

        var totalCount = await dbQuery.CountAsync();
        var totalPages = (int)Math.Ceiling(totalCount / (double)query.PageSize);

        var transformations = await dbQuery
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync();

        return new PagedTransformationResponse
        {
            Transformations = transformations.Select(MapToListResponse).ToList(),
            TotalCount = totalCount,
            Page = query.Page,
            PageSize = query.PageSize,
            TotalPages = totalPages
        };
    }

    /// <summary>
    /// Handle GetTransformationsByPipelineIdQuery
    /// </summary>
    public Task<IEnumerable<Transformation>> Handle(GetTransformationsByPipelineIdQuery query)
    {
        _logger.LogInformation("Getting transformations for pipeline {PipelineId} - NOTE: Transformations are now embedded in field mappings", query.PipelineId);

        // Transformations are now embedded in field mappings, not stored separately per pipeline
        // Return empty list for backward compatibility
        return Task.FromResult<IEnumerable<Transformation>>(Enumerable.Empty<Transformation>());
    }

    private static IQueryable<Transformation> ApplySorting(IQueryable<Transformation> query, string? sort)
    {
        return sort switch
        {
            "name_asc" => query.OrderBy(t => t.Name),
            "name_desc" => query.OrderByDescending(t => t.Name),
            "type_asc" => query.OrderBy(t => t.Type).ThenBy(t => t.Name),
            "created_asc" => query.OrderBy(t => t.CreatedAt),
            "created_desc" => query.OrderByDescending(t => t.CreatedAt),
            _ => query.OrderByDescending(t => t.CreatedAt)
        };
    }

    private static TransformationResponse MapToResponse(Transformation transformation)
    {
        return new TransformationResponse
        {
            Id = transformation.Id,
            TenantId = transformation.TenantId,
            Name = transformation.Name,
            Description = transformation.Description,
            Type = transformation.Type,
            Config = JsonSerializer.Deserialize<JsonElement>(transformation.ConfigJson),
            CreatedAt = transformation.CreatedAt,
            UpdatedAt = transformation.UpdatedAt
        };
    }

    private static TransformationListResponse MapToListResponse(Transformation transformation)
    {
        return new TransformationListResponse
        {
            Id = transformation.Id,
            Name = transformation.Name,
            Description = transformation.Description,
            Type = transformation.Type,
            Config = JsonSerializer.Deserialize<JsonElement>(transformation.ConfigJson),
            CreatedAt = transformation.CreatedAt
        };
    }
}
