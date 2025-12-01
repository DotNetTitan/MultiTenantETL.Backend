using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MultiTenantETL.Application.Common.Interfaces;
using MultiTenantETL.Application.Interfaces;
using MultiTenantETL.Application.Transformations;
using MultiTenantETL.Application.Transformations.Models;
using MultiTenantETL.Domain.Constants;
using MultiTenantETL.Domain.Entities;
using MultiTenantETL.Infrastructure.Persistence;

namespace MultiTenantETL.Infrastructure.Services;

public class TransformationService : ITransformationService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<TransformationService> _logger;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAuditService _auditService;

    private static readonly string[] ValidTransformationTypes = new[]
    {
        "Filter", "Map", "Trim", "Case Convert", "Substring", "Replace", "Script"
    };

    public TransformationService(
        ApplicationDbContext context,
        ILogger<TransformationService> logger,
        ICurrentUserService currentUserService,
        IAuditService auditService)
    {
        _context = context;
        _logger = logger;
        _currentUserService = currentUserService;
        _auditService = auditService;
    }

    public async Task<TransformationResponse> CreateAsync(CreateTransformationRequest request, CancellationToken cancellationToken = default)
    {
        var tenantId = _currentUserService.GetTenantId();
        var userId = _currentUserService.GetUserId();

        _logger.LogInformation("Creating transformation {Name} for tenant {TenantId}", request.Name, tenantId);

        // Validate transformation type
        if (!ValidTransformationTypes.Contains(request.Type))
        {
            throw new ArgumentException($"Invalid transformation type: {request.Type}. Valid types are: {string.Join(", ", ValidTransformationTypes)}");
        }

        var transformation = new Transformation
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = request.Name,
            Description = request.Description,
            Type = request.Type,
            ConfigJson = JsonSerializer.Serialize(request.Config),
            CreatedAt = DateTime.UtcNow,
            CreatedBy = userId
        };

        _context.Transformations.Add(transformation);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Transformation {TransformationId} created successfully", transformation.Id);

        // Audit log
        await _auditService.LogAsync(
            action: AuditActions.TransformationCreated,
            resourceType: "Transformation",
            resourceId: transformation.Id.ToString(),
            description: $"Created transformation '{transformation.Name}' ({transformation.Type})",
            metadata: new { transformation.Type }
        );

        return MapToResponse(transformation);
    }

    public async Task<TransformationResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tenantId = _currentUserService.GetTenantId();

        var transformation = await _context.Transformations
            .Where(t => t.Id == id && t.TenantId == tenantId)
            .FirstOrDefaultAsync(cancellationToken);

        if (transformation == null)
        {
            throw new KeyNotFoundException($"Transformation with ID {id} not found");
        }

        return MapToResponse(transformation);
    }

    public async Task<PagedTransformationResponse> GetAllAsync(TransformationSearchRequest request, CancellationToken cancellationToken = default)
    {
        var tenantId = _currentUserService.GetTenantId();

        var query = _context.Transformations
            .Where(t => t.TenantId == tenantId);

        // Apply search filter
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            query = query.Where(t => t.Name.Contains(request.Search) || 
                                    (t.Description != null && t.Description.Contains(request.Search)));
        }

        // Apply name filter
        if (!string.IsNullOrWhiteSpace(request.Name))
        {
            query = query.Where(t => t.Name.Contains(request.Name));
        }

        // Apply type filter
        if (!string.IsNullOrWhiteSpace(request.Type) && request.Type != "All")
        {
            query = query.Where(t => t.Type == request.Type);
        }

        // Apply sorting
        query = ApplySorting(query, request.Sort);

        var totalCount = await query.CountAsync(cancellationToken);
        var totalPages = (int)Math.Ceiling(totalCount / (double)request.PageSize);

        var transformations = await query
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedTransformationResponse
        {
            Transformations = transformations.Select(MapToListResponse).ToList(),
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize,
            TotalPages = totalPages
        };
    }

    public async Task<TransformationResponse> UpdateAsync(Guid id, UpdateTransformationRequest request, CancellationToken cancellationToken = default)
    {
        var tenantId = _currentUserService.GetTenantId();
        var userId = _currentUserService.GetUserId();

        var transformation = await _context.Transformations
            .Where(t => t.Id == id && t.TenantId == tenantId)
            .FirstOrDefaultAsync(cancellationToken);

        if (transformation == null)
        {
            throw new KeyNotFoundException($"Transformation with ID {id} not found");
        }

        _logger.LogInformation("Updating transformation {TransformationId}", id);

        var oldName = transformation.Name;

        transformation.Name = request.Name;
        transformation.Description = request.Description;
        transformation.ConfigJson = JsonSerializer.Serialize(request.Config);
        transformation.UpdatedAt = DateTime.UtcNow;
        transformation.UpdatedBy = userId;

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Transformation {TransformationId} updated successfully", id);

        // Audit log
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

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tenantId = _currentUserService.GetTenantId();

        var transformation = await _context.Transformations
            .Where(t => t.Id == id && t.TenantId == tenantId)
            .FirstOrDefaultAsync(cancellationToken);

        if (transformation == null)
        {
            throw new KeyNotFoundException($"Transformation with ID {id} not found");
        }

        _logger.LogInformation("Deleting transformation {TransformationId}", id);

        var transformationName = transformation.Name;
        var transformationType = transformation.Type;

        _context.Transformations.Remove(transformation);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Transformation {TransformationId} deleted successfully", id);

        // Audit log
        await _auditService.LogAsync(
            action: AuditActions.TransformationDeleted,
            resourceType: "Transformation",
            resourceId: id.ToString(),
            description: $"Deleted transformation '{transformationName}' ({transformationType})",
            metadata: new { Type = transformationType }
        );
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
            _ => query.OrderByDescending(t => t.CreatedAt) // Default sort
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

    public async Task<IEnumerable<Transformation>> GetByPipelineIdAsync(Guid pipelineId, CancellationToken cancellationToken = default)
    {
        var tenantId = _currentUserService.GetTenantId();

        _logger.LogInformation("Getting transformations for pipeline {PipelineId}", pipelineId);

        var transformations = await _context.Transformations
            .Where(t => t.PipelineId == pipelineId && t.TenantId == tenantId && t.IsEnabled)
            .OrderBy(t => t.Order)
            .ToListAsync(cancellationToken);

        return transformations;
    }
}
