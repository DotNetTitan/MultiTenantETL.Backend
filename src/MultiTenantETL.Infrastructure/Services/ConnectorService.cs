using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MultiTenantETL.Application.Common.Interfaces;
using MultiTenantETL.Application.Connectors;
using MultiTenantETL.Application.Connectors.Models;
using MultiTenantETL.Application.Interfaces;
using MultiTenantETL.Domain.Constants;
using MultiTenantETL.Domain.Entities;
using MultiTenantETL.Infrastructure.Persistence;

namespace MultiTenantETL.Infrastructure.Services;

public class ConnectorService : IConnectorService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<ConnectorService> _logger;
    private readonly IConnectionTester _connectionTester;
    private readonly ISchemaDetector _schemaDetector;
    private readonly IEncryptionService _encryptionService;
    private readonly IAuditService _auditService;

    public ConnectorService(
        ApplicationDbContext context,
        ILogger<ConnectorService> logger,
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

    public async Task<ConnectorResponse> CreateAsync(CreateConnectorRequest request, Guid tenantId, Guid userId)
    {
        _logger.LogInformation("Creating connector {Name} for tenant {TenantId}", request.Name, tenantId);

        // Validate type and provider
        ValidateTypeAndProvider(request.Type, request.Provider);

        // Determine direction flags
        var (isSource, isDestination) = ParseDirection(request.Direction);

        // Encrypt sensitive fields in config
        var encryptedConfig = _encryptionService.EncryptJsonFields(request.Config, EncryptionConstants.SensitiveFields);

        var connector = new Connector
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = request.Name,
            Description = request.Description,
            Type = request.Type,
            Provider = request.Provider,
            Direction = request.Direction,
            IsSource = isSource,
            IsDestination = isDestination,
            RequiresCredentials = DetermineRequiresCredentials(request.Type, request.Provider),
            ConfigJson = JsonSerializer.Serialize(encryptedConfig),
            SchemaJson = request.Schema.HasValue ? JsonSerializer.Serialize(request.Schema.Value) : null,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = userId
        };

        _context.Connectors.Add(connector);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Connector {ConnectorId} created successfully", connector.Id);

        // Audit log
        await _auditService.LogAsync(
            action: AuditActions.ConnectorCreated,
            resourceType: "Connector",
            resourceId: connector.Id.ToString(),
            description: $"Created connector '{connector.Name}' ({connector.Type}/{connector.Provider})",
            metadata: new { connector.Type, connector.Provider, connector.Direction }
        );

        return MapToResponse(connector);
    }

    public async Task<ConnectorResponse> GetByIdAsync(Guid id, Guid tenantId)
    {
        var connector = await _context.Connectors
            .Where(c => c.Id == id && c.TenantId == tenantId)
            .FirstOrDefaultAsync();

        if (connector == null)
        {
            throw new KeyNotFoundException($"Connector with ID {id} not found");
        }

        return MapToResponse(connector);
    }

    public async Task<PagedConnectorResponse> SearchAsync(ConnectorSearchRequest request, Guid tenantId)
    {
        var query = _context.Connectors
            .Where(c => c.TenantId == tenantId);

        // Apply filters
        if (!string.IsNullOrWhiteSpace(request.Name))
        {
            query = query.Where(c => c.Name.Contains(request.Name));
        }

        if (!string.IsNullOrWhiteSpace(request.Type))
        {
            query = query.Where(c => c.Type == request.Type);
        }

        if (!string.IsNullOrWhiteSpace(request.Provider))
        {
            query = query.Where(c => c.Provider == request.Provider);
        }

        if (!string.IsNullOrWhiteSpace(request.Direction))
        {
            query = query.Where(c => c.Direction == request.Direction);
        }

        if (request.IsActive.HasValue)
        {
            query = query.Where(c => c.IsActive == request.IsActive.Value);
        }

        var totalCount = await query.CountAsync();
        var totalPages = (int)Math.Ceiling(totalCount / (double)request.PageSize);

        var connectors = await query
            .OrderByDescending(c => c.CreatedAt)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync();

        try
        {
            return new PagedConnectorResponse
            {
                Connectors = connectors.Select(MapToListResponse).ToList(),
                TotalCount = totalCount,
                Page = request.Page,
                PageSize = request.PageSize,
                TotalPages = totalPages
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error mapping connectors to response");
            throw;
        }
    }

    public async Task<ConnectorResponse> UpdateAsync(Guid id, UpdateConnectorRequest request, Guid tenantId, Guid userId)
    {
        var connector = await _context.Connectors
            .Where(c => c.Id == id && c.TenantId == tenantId)
            .FirstOrDefaultAsync();

        if (connector == null)
        {
            throw new KeyNotFoundException($"Connector with ID {id} not found");
        }

        _logger.LogInformation("Updating connector {ConnectorId}", id);

        var oldName = connector.Name;
        var oldIsActive = connector.IsActive;

        connector.Name = request.Name;
        connector.Description = request.Description;
        connector.Direction = request.Direction;
        
        var (isSource, isDestination) = ParseDirection(request.Direction);
        connector.IsSource = isSource;
        connector.IsDestination = isDestination;
        
        // Encrypt sensitive fields in config
        var encryptedConfig = _encryptionService.EncryptJsonFields(request.Config, EncryptionConstants.SensitiveFields);
        connector.ConfigJson = JsonSerializer.Serialize(encryptedConfig);
        connector.SchemaJson = request.Schema.HasValue ? JsonSerializer.Serialize(request.Schema.Value) : connector.SchemaJson;
        
        if (request.IsActive.HasValue)
        {
            connector.IsActive = request.IsActive.Value;
        }
        
        connector.UpdatedAt = DateTime.UtcNow;
        connector.UpdatedBy = userId;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Connector {ConnectorId} updated successfully", id);

        // Audit log
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

    public async Task DeleteAsync(Guid id, Guid tenantId)
    {
        var connector = await _context.Connectors
            .Where(c => c.Id == id && c.TenantId == tenantId)
            .FirstOrDefaultAsync();

        if (connector == null)
        {
            throw new KeyNotFoundException($"Connector with ID {id} not found");
        }

        _logger.LogInformation("Deleting connector {ConnectorId}", id);

        var connectorName = connector.Name;
        var connectorType = connector.Type;

        _context.Connectors.Remove(connector);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Connector {ConnectorId} deleted successfully", id);

        // Audit log
        await _auditService.LogAsync(
            action: AuditActions.ConnectorDeleted,
            resourceType: "Connector",
            resourceId: id.ToString(),
            description: $"Deleted connector '{connectorName}' ({connectorType})",
            metadata: new { Name = connectorName, Type = connectorType }
        );
    }

    public async Task<TestConnectionResponse> TestConnectionAsync(TestConnectionRequest request, Guid tenantId)
    {
        _logger.LogInformation("Testing connection for type {Type}, provider {Provider}", request.Type, request.Provider);

        ValidateTypeAndProvider(request.Type, request.Provider);

        // For testing a NEW connection, the config comes in plain text from the request,
        // so no decryption is needed (it was never encrypted)
        var result = await _connectionTester.TestConnectionAsync(request.Type, request.Provider, request.Config);

        // Audit log
        await _auditService.LogAsync(
            action: AuditActions.ConnectorTested,
            resourceType: "Connector",
            description: $"Tested new connection ({request.Type}/{request.Provider})",
            metadata: new { request.Type, request.Provider, result.Success },
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

    public async Task<TestConnectionResponse> TestExistingConnectionAsync(Guid id, Guid tenantId)
    {
        var connector = await _context.Connectors
            .Where(c => c.Id == id && c.TenantId == tenantId)
            .FirstOrDefaultAsync();

        if (connector == null)
        {
            throw new KeyNotFoundException($"Connector with ID {id} not found");
        }

        _logger.LogInformation("Testing existing connector {ConnectorId}", id);

        var config = JsonSerializer.Deserialize<JsonElement>(connector.ConfigJson);
        
        // Decrypt sensitive fields before testing
        var decryptedConfig = _encryptionService.DecryptJsonFields(config, EncryptionConstants.SensitiveFields);
        var result = await _connectionTester.TestConnectionAsync(connector.Type, connector.Provider, decryptedConfig);

        // Update connector with test results
        connector.LastTestedAt = DateTime.UtcNow;
        connector.LastTestResult = result.Success ? TestResults.Success : TestResults.Failed;
        connector.LastTestMessage = result.Message;
        await _context.SaveChangesAsync();

        // Audit log
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

    public async Task<DetectSchemaResponse> DetectSchemaAsync(DetectSchemaRequest request, Guid tenantId)
    {
        var connector = await _context.Connectors
            .Where(c => c.Id == request.ConnectorId && c.TenantId == tenantId)
            .FirstOrDefaultAsync();

        if (connector == null)
        {
            throw new KeyNotFoundException($"Connector with ID {request.ConnectorId} not found");
        }

        _logger.LogInformation("Detecting schema for connector {ConnectorId}", request.ConnectorId);

        var config = JsonSerializer.Deserialize<JsonElement>(connector.ConfigJson);
        
        // Decrypt sensitive fields before schema detection
        var decryptedConfig = _encryptionService.DecryptJsonFields(config, EncryptionConstants.SensitiveFields);
        var result = await _schemaDetector.DetectSchemaAsync(
            connector.Type,
            connector.Provider,
            decryptedConfig,
            request.TableOrResourceName);

        if (result.Success && result.Schema != null)
        {
            // Update connector with detected schema
            connector.SchemaJson = JsonSerializer.Serialize(result.Schema);
            await _context.SaveChangesAsync();

            // Audit log
            await _auditService.LogAsync(
                action: AuditActions.ConnectorSchemaDetected,
                resourceType: "Connector",
                resourceId: connector.Id.ToString(),
                description: $"Detected schema for connector '{connector.Name}'",
                metadata: new { connector.Name, TableOrResource = request.TableOrResourceName }
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

    public async Task<DetectSchemaResponse> DetectSchemaPreviewAsync(DetectSchemaPreviewRequest request, Guid tenantId)
    {
        _logger.LogInformation("Detecting schema preview for type {Type}, provider {Provider}", request.Type, request.Provider);

        ValidateTypeAndProvider(request.Type, request.Provider);

        // For schema preview of a NEW connection, the config comes in plain text from the request,
        // so no decryption is needed (it was never encrypted)
        var result = await _schemaDetector.DetectSchemaAsync(
            request.Type,
            request.Provider,
            request.Config,
            request.TableOrResourceName);

        // Audit log
        await _auditService.LogAsync(
            action: AuditActions.ConnectorSchemaDetected,
            resourceType: "Connector",
            description: $"Detected schema preview ({request.Type}/{request.Provider})",
            metadata: new { request.Type, request.Provider, TableOrResource = request.TableOrResourceName, result.Success }
        );

        return new DetectSchemaResponse
        {
            Success = result.Success,
            Message = result.Message,
            Schema = result.Schema,
            DetectedAt = DateTime.UtcNow
        };
    }

    public async Task<List<ConnectorListResponse>> GetAllAsync(Guid tenantId)
    {
        var connectors = await _context.Connectors
            .Where(c => c.TenantId == tenantId && c.IsActive)
            .OrderBy(c => c.Name)
            .ToListAsync();

        return connectors.Select(MapToListResponse).ToList();
    }

    // Helper methods
    private static void ValidateTypeAndProvider(string type, string provider)
    {
        var validTypes = new[] { ConnectorTypes.Database, ConnectorTypes.File, ConnectorTypes.Api };
        if (!validTypes.Contains(type))
        {
            throw new ArgumentException($"Invalid connector type: {type}");
        }

        var validProviders = type switch
        {
            ConnectorTypes.Database => new[] { ConnectorProviders.SqlServer, ConnectorProviders.PostgreSQL, ConnectorProviders.MySQL, ConnectorProviders.Oracle, ConnectorProviders.Snowflake, ConnectorProviders.BigQuery, ConnectorProviders.Redshift, ConnectorProviders.MongoDb },
            ConnectorTypes.File => new[] { ConnectorProviders.Local, ConnectorProviders.FTP, ConnectorProviders.SFTP, ConnectorProviders.S3, ConnectorProviders.AzureBlob, ConnectorProviders.GCS },
            ConnectorTypes.Api => new[] { ConnectorProviders.REST },
            _ => Array.Empty<string>()
        };

        if (!validProviders.Contains(provider))
        {
            throw new ArgumentException($"Invalid provider '{provider}' for type '{type}'");
        }
    }

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
        // Most connectors require credentials except local files
        return !(type == ConnectorTypes.File && provider == ConnectorProviders.Local);
    }

    private ConnectorResponse MapToResponse(Connector connector)
    {
        var config = JsonSerializer.Deserialize<JsonElement>(connector.ConfigJson);
        
        // Decrypt sensitive fields for response
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
