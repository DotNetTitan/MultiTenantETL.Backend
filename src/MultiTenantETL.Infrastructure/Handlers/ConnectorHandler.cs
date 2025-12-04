using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MultiTenantETL.Application.Common.Interfaces;
using MultiTenantETL.Application.Connectors;
using MultiTenantETL.Application.Connectors.Commands;
using MultiTenantETL.Application.Connectors.Models;
using MultiTenantETL.Application.Connectors.Queries;
using MultiTenantETL.Application.Interfaces;
using MultiTenantETL.Domain.Constants;
using MultiTenantETL.Domain.Entities;
using MultiTenantETL.Infrastructure.Persistence;

namespace MultiTenantETL.Infrastructure.Handlers;

/// <summary>
/// Wolverine handlers for Connector commands and queries
/// </summary>
public class ConnectorHandler
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<ConnectorHandler> _logger;
    private readonly IConnectionTester _connectionTester;
    private readonly ISchemaDetector _schemaDetector;
    private readonly IEncryptionService _encryptionService;
    private readonly IAuditService _auditService;

    public ConnectorHandler(
        ApplicationDbContext context,
        ILogger<ConnectorHandler> logger,
        IConnectionTester connectionTester,
        ISchemaDetector schemaDetector,
        IEncryptionService encryptionService,
        IAuditService auditService)
    {
        _context = context;
        _logger = logger;
        _connectionTester = connectionTester;
        _schemaDetector = schemaDetector;
        _encryptionService = encryptionService;
        _auditService = auditService;
    }

    /// <summary>
    /// Handle CreateConnectorCommand
    /// </summary>
    public async Task<ConnectorResponse> Handle(CreateConnectorCommand command)
    {
        _logger.LogInformation("Creating connector {Name} for tenant {TenantId}", command.Name, command.TenantId);

        var (isSource, isDestination) = ParseDirection(command.Direction);

        var encryptedConfig = _encryptionService.EncryptJsonFields(command.Config, EncryptionConstants.SensitiveFields);

        var connector = new Connector
        {
            Id = Guid.NewGuid(),
            TenantId = command.TenantId,
            Name = command.Name,
            Description = command.Description,
            Type = command.Type,
            Provider = command.Provider,
            Direction = command.Direction,
            IsSource = isSource,
            IsDestination = isDestination,
            RequiresCredentials = DetermineRequiresCredentials(command.Type, command.Provider),
            ConfigJson = JsonSerializer.Serialize(encryptedConfig),
            SchemaJson = command.Schema.HasValue ? JsonSerializer.Serialize(command.Schema.Value) : null,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = command.UserId
        };

        _context.Connectors.Add(connector);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Connector {ConnectorId} created successfully", connector.Id);

        await _auditService.LogAsync(
            action: AuditActions.ConnectorCreated,
            resourceType: "Connector",
            resourceId: connector.Id.ToString(),
            description: $"Created connector '{connector.Name}' ({connector.Type}/{connector.Provider})",
            metadata: new { connector.Type, connector.Provider, connector.Direction }
        );

        return MapToResponse(connector);
    }

    /// <summary>
    /// Handle UpdateConnectorCommand
    /// </summary>
    public async Task<ConnectorResponse> Handle(UpdateConnectorCommand command)
    {
        var connector = await _context.Connectors
            .Where(c => c.Id == command.Id && c.TenantId == command.TenantId)
            .FirstOrDefaultAsync();

        if (connector == null)
        {
            throw new KeyNotFoundException($"Connector with ID {command.Id} not found");
        }

        _logger.LogInformation("Updating connector {ConnectorId}", command.Id);

        var oldName = connector.Name;
        var oldIsActive = connector.IsActive;

        connector.Name = command.Name;
        connector.Description = command.Description;
        connector.Direction = command.Direction;

        var (isSource, isDestination) = ParseDirection(command.Direction);
        connector.IsSource = isSource;
        connector.IsDestination = isDestination;

        var encryptedConfig = _encryptionService.EncryptJsonFields(command.Config, EncryptionConstants.SensitiveFields);
        connector.ConfigJson = JsonSerializer.Serialize(encryptedConfig);
        connector.SchemaJson = command.Schema.HasValue ? JsonSerializer.Serialize(command.Schema.Value) : connector.SchemaJson;

        if (command.IsActive.HasValue)
        {
            connector.IsActive = command.IsActive.Value;
        }

        connector.UpdatedAt = DateTime.UtcNow;
        connector.UpdatedBy = command.UserId;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Connector {ConnectorId} updated successfully", command.Id);

        var changes = new List<string>();
        if (oldName != connector.Name) changes.Add($"name: '{oldName}' → '{connector.Name}'");
        if (oldIsActive != connector.IsActive) changes.Add($"active: {oldIsActive} → {connector.IsActive}");

        await _auditService.LogAsync(
            action: AuditActions.ConnectorUpdated,
            resourceType: "Connector",
            resourceId: connector.Id.ToString(),
            description: $"Updated connector '{connector.Name}'",
            metadata: new { Changes = changes }
        );

        return MapToResponse(connector);
    }

    /// <summary>
    /// Handle DeleteConnectorCommand
    /// </summary>
    public async Task Handle(DeleteConnectorCommand command)
    {
        var connector = await _context.Connectors
            .Where(c => c.Id == command.Id && c.TenantId == command.TenantId)
            .FirstOrDefaultAsync();

        if (connector == null)
        {
            throw new KeyNotFoundException($"Connector with ID {command.Id} not found");
        }

        _logger.LogInformation("Deleting connector {ConnectorId}", command.Id);

        var connectorName = connector.Name;
        var connectorType = connector.Type;

        _context.Connectors.Remove(connector);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Connector {ConnectorId} deleted successfully", command.Id);

        await _auditService.LogAsync(
            action: AuditActions.ConnectorDeleted,
            resourceType: "Connector",
            resourceId: command.Id.ToString(),
            description: $"Deleted connector '{connectorName}' ({connectorType})",
            metadata: new { Name = connectorName, Type = connectorType }
        );
    }

    /// <summary>
    /// Handle TestConnectionCommand
    /// </summary>
    public async Task<TestConnectionResponse> Handle(TestConnectionCommand command)
    {
        _logger.LogInformation("Testing connection for type {Type}, provider {Provider}", command.Type, command.Provider);

        var result = await _connectionTester.TestConnectionAsync(command.Type, command.Provider, command.Config);

        await _auditService.LogAsync(
            action: AuditActions.ConnectorTested,
            resourceType: "Connector",
            description: $"Tested new connection ({command.Type}/{command.Provider})",
            metadata: new { command.Type, command.Provider, result.Success },
            success: result.Success
        );

        return new TestConnectionResponse
        {
            Success = result.Success,
            Message = result.Message,
            TestedAt = DateTime.UtcNow,
            Details = result.Details
        };
    }

    /// <summary>
    /// Handle TestExistingConnectionCommand
    /// </summary>
    public async Task<TestConnectionResponse> Handle(TestExistingConnectionCommand command)
    {
        var connector = await _context.Connectors
            .Where(c => c.Id == command.Id && c.TenantId == command.TenantId)
            .FirstOrDefaultAsync();

        if (connector == null)
        {
            throw new KeyNotFoundException($"Connector with ID {command.Id} not found");
        }

        _logger.LogInformation("Testing existing connector {ConnectorId}", command.Id);

        var config = JsonSerializer.Deserialize<JsonElement>(connector.ConfigJson);
        var decryptedConfig = _encryptionService.DecryptJsonFields(config, EncryptionConstants.SensitiveFields);
        var result = await _connectionTester.TestConnectionAsync(connector.Type, connector.Provider, decryptedConfig);

        connector.LastTestedAt = DateTime.UtcNow;
        connector.LastTestResult = result.Success ? TestResults.Success : TestResults.Failed;
        connector.LastTestMessage = result.Message;
        await _context.SaveChangesAsync();

        await _auditService.LogAsync(
            action: AuditActions.ConnectorTested,
            resourceType: "Connector",
            resourceId: connector.Id.ToString(),
            description: $"Tested connector '{connector.Name}'",
            metadata: new { connector.Name, result.Success },
            success: result.Success
        );

        return new TestConnectionResponse
        {
            Success = result.Success,
            Message = result.Message,
            TestedAt = DateTime.UtcNow,
            Details = result.Details
        };
    }

    /// <summary>
    /// Handle DetectSchemaCommand
    /// </summary>
    public async Task<DetectSchemaResponse> Handle(DetectSchemaCommand command)
    {
        var connector = await _context.Connectors
            .Where(c => c.Id == command.ConnectorId && c.TenantId == command.TenantId)
            .FirstOrDefaultAsync();

        if (connector == null)
        {
            throw new KeyNotFoundException($"Connector with ID {command.ConnectorId} not found");
        }

        _logger.LogInformation("Detecting schema for connector {ConnectorId}", command.ConnectorId);

        var config = JsonSerializer.Deserialize<JsonElement>(connector.ConfigJson);
        var decryptedConfig = _encryptionService.DecryptJsonFields(config, EncryptionConstants.SensitiveFields);
        var result = await _schemaDetector.DetectSchemaAsync(
            connector.Type,
            connector.Provider,
            decryptedConfig,
            command.TableOrResourceName);

        if (result.Success && result.Schema != null)
        {
            connector.SchemaJson = JsonSerializer.Serialize(result.Schema);
            await _context.SaveChangesAsync();

            await _auditService.LogAsync(
                action: AuditActions.ConnectorSchemaDetected,
                resourceType: "Connector",
                resourceId: connector.Id.ToString(),
                description: $"Detected schema for connector '{connector.Name}'",
                metadata: new { connector.Name, TableOrResource = command.TableOrResourceName }
            );
        }

        return new DetectSchemaResponse
        {
            Success = result.Success,
            Message = result.Message,
            Schema = result.Schema,
            DetectedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Handle DetectSchemaPreviewCommand
    /// </summary>
    public async Task<DetectSchemaResponse> Handle(DetectSchemaPreviewCommand command)
    {
        _logger.LogInformation("Detecting schema preview for type {Type}, provider {Provider}", command.Type, command.Provider);

        var result = await _schemaDetector.DetectSchemaAsync(
            command.Type,
            command.Provider,
            command.Config,
            command.TableOrResourceName);

        await _auditService.LogAsync(
            action: AuditActions.ConnectorSchemaDetected,
            resourceType: "Connector",
            description: $"Detected schema preview ({command.Type}/{command.Provider})",
            metadata: new { command.Type, command.Provider, TableOrResource = command.TableOrResourceName, result.Success }
        );

        return new DetectSchemaResponse
        {
            Success = result.Success,
            Message = result.Message,
            Schema = result.Schema,
            DetectedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Handle GetConnectorByIdQuery
    /// </summary>
    public async Task<ConnectorResponse> Handle(GetConnectorByIdQuery query)
    {
        var connector = await _context.Connectors
            .Where(c => c.Id == query.Id && c.TenantId == query.TenantId)
            .FirstOrDefaultAsync();

        if (connector == null)
        {
            throw new KeyNotFoundException($"Connector with ID {query.Id} not found");
        }

        return MapToResponse(connector);
    }

    /// <summary>
    /// Handle SearchConnectorsQuery
    /// </summary>
    public async Task<PagedConnectorResponse> Handle(SearchConnectorsQuery query)
    {
        var dbQuery = _context.Connectors
            .Where(c => c.TenantId == query.TenantId);

        if (!string.IsNullOrWhiteSpace(query.Name))
        {
            dbQuery = dbQuery.Where(c => c.Name.Contains(query.Name));
        }

        if (!string.IsNullOrWhiteSpace(query.Type))
        {
            dbQuery = dbQuery.Where(c => c.Type == query.Type);
        }

        if (!string.IsNullOrWhiteSpace(query.Provider))
        {
            dbQuery = dbQuery.Where(c => c.Provider == query.Provider);
        }

        if (!string.IsNullOrWhiteSpace(query.Direction))
        {
            dbQuery = dbQuery.Where(c => c.Direction == query.Direction);
        }

        if (query.IsActive.HasValue)
        {
            dbQuery = dbQuery.Where(c => c.IsActive == query.IsActive.Value);
        }

        var totalCount = await dbQuery.CountAsync();
        var totalPages = (int)Math.Ceiling(totalCount / (double)query.PageSize);

        var connectors = await dbQuery
            .OrderByDescending(c => c.CreatedAt)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync();

        return new PagedConnectorResponse
        {
            Connectors = connectors.Select(MapToListResponse).ToList(),
            TotalCount = totalCount,
            Page = query.Page,
            PageSize = query.PageSize,
            TotalPages = totalPages
        };
    }

    /// <summary>
    /// Handle GetAllConnectorsQuery
    /// </summary>
    public async Task<List<ConnectorListResponse>> Handle(GetAllConnectorsQuery query)
    {
        var connectors = await _context.Connectors
            .Where(c => c.TenantId == query.TenantId && c.IsActive)
            .OrderBy(c => c.Name)
            .ToListAsync();

        return connectors.Select(MapToListResponse).ToList();
    }

    // Helper methods
    private static (bool isSource, bool isDestination) ParseDirection(string direction)
    {
        return direction.ToLower() switch
        {
            ConnectorDirections.Source => (true, false),
            ConnectorDirections.Destination => (false, true),
            ConnectorDirections.Both => (true, true),
            _ => throw new ArgumentException($"Invalid direction: {direction}")
        };
    }

    private static bool DetermineRequiresCredentials(string type, string provider)
    {
        return !(type == ConnectorTypes.File && provider == ConnectorProviders.Local);
    }

    private ConnectorResponse MapToResponse(Connector connector)
    {
        var config = JsonSerializer.Deserialize<JsonElement>(connector.ConfigJson);
        var decryptedConfig = _encryptionService.DecryptJsonFields(config, EncryptionConstants.SensitiveFields);

        return new ConnectorResponse
        {
            Id = connector.Id,
            TenantId = connector.TenantId,
            Name = connector.Name,
            Description = connector.Description,
            Type = connector.Type,
            Provider = connector.Provider,
            Direction = connector.Direction,
            IsSource = connector.IsSource,
            IsDestination = connector.IsDestination,
            RequiresCredentials = connector.RequiresCredentials,
            IsActive = connector.IsActive,
            Config = decryptedConfig,
            Schema = !string.IsNullOrEmpty(connector.SchemaJson)
                ? JsonSerializer.Deserialize<JsonElement>(connector.SchemaJson)
                : null,
            LastTestedAt = connector.LastTestedAt,
            LastTestResult = connector.LastTestResult,
            LastTestMessage = connector.LastTestMessage,
            CreatedAt = connector.CreatedAt,
            UpdatedAt = connector.UpdatedAt
        };
    }

    private static ConnectorListResponse MapToListResponse(Connector connector)
    {
        return new ConnectorListResponse
        {
            Id = connector.Id,
            Name = connector.Name,
            Description = connector.Description,
            Type = connector.Type,
            Provider = connector.Provider,
            Direction = connector.Direction,
            IsSource = connector.IsSource,
            IsDestination = connector.IsDestination,
            IsActive = connector.IsActive,
            LastTestedAt = connector.LastTestedAt,
            LastTestResult = connector.LastTestResult,
            CreatedAt = connector.CreatedAt
        };
    }
}
