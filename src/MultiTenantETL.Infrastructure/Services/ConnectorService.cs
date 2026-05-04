using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MultiTenantETL.Application.Common.Interfaces;
using MultiTenantETL.Application.Connectors;
using MultiTenantETL.Application.Connectors.Models;
using MultiTenantETL.Application.Interfaces;
using MultiTenantETL.Domain.Constants;
using MultiTenantETL.Domain.Entities;
using MultiTenantETL.Infrastructure.Persistence;
using System.Text.Json;

namespace MultiTenantETL.Infrastructure.Services;

public class ConnectorService : IConnectorService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<ConnectorService> _logger;
    private readonly IConnectionTester _connectionTester;
    private readonly ISchemaDetector _schemaDetector;
    private readonly ISecretStorageService _secretStorageService;
    private readonly ISecretResolver _secretResolver;
    private readonly IAuditService _auditService;

    public ConnectorService(
        ApplicationDbContext context,
        ILogger<ConnectorService> logger,
        IConnectionTester connectionTester,
        ISchemaDetector schemaDetector,
        ISecretStorageService secretStorageService,
        ISecretResolver secretResolver,
        IAuditService auditService)
    {
        _context = context;
        _logger = logger;
        _connectionTester = connectionTester;
        _schemaDetector = schemaDetector;
        _secretStorageService = secretStorageService;
        _secretResolver = secretResolver;
        _auditService = auditService;
    }

    public async Task<ConnectorResponse> CreateAsync(CreateConnectorRequest request, Guid tenantId, Guid userId)
    {
        _logger.LogInformation("Creating connector {Name} for tenant {TenantId}", request.Name, tenantId);

        // Validate type and provider
        ValidateTypeAndProvider(request.Type, request.Provider);

        // Determine direction flags
        var (isSource, isDestination) = ParseDirection(request.Direction);

        var connectorId = Guid.NewGuid();

        // Store sensitive fields in Key Vault and replace with references
        var configWithReferences = await StoreSecretsInKeyVaultAsync(tenantId, connectorId, request.Config);

        var connector = new Connector
        {
            Id = connectorId,
            TenantId = tenantId,
            Name = request.Name,
            Description = request.Description,
            Type = request.Type,
            Provider = request.Provider,
            Direction = request.Direction,
            IsSource = isSource,
            IsDestination = isDestination,
            RequiresCredentials = DetermineRequiresCredentials(request.Type, request.Provider),
            ConfigJson = JsonSerializer.Serialize(configWithReferences),
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

        // Validate that Type and Provider haven't changed (they are immutable)
        if (connector.Type != request.Type)
        {
            throw new InvalidOperationException($"Cannot change connector type from '{connector.Type}' to '{request.Type}'. Type is immutable after creation.");
        }

        if (connector.Provider != request.Provider)
        {
            throw new InvalidOperationException($"Cannot change connector provider from '{connector.Provider}' to '{request.Provider}'. Provider is immutable after creation.");
        }

        var oldName = connector.Name;
        var oldIsActive = connector.IsActive;

        connector.Name = request.Name;
        connector.Description = request.Description;
        connector.Direction = request.Direction;

        var (isSource, isDestination) = ParseDirection(request.Direction);
        connector.IsSource = isSource;
        connector.IsDestination = isDestination;

        // Store sensitive fields in Key Vault and replace with references
        var configWithReferences = await StoreSecretsInKeyVaultAsync(tenantId, id, request.Config);
        connector.ConfigJson = JsonSerializer.Serialize(configWithReferences);
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

        // Check if connector is being used by any pipelines
        var pipelinesUsingConnector = await _context.Pipelines
            .Where(p => p.TenantId == tenantId &&
                       (p.SourceConnectorId == id || p.DestinationConnectorId == id))
            .Select(p => p.Name)
            .ToListAsync();

        if (pipelinesUsingConnector.Any())
        {
            var pipelineList = string.Join(", ", pipelinesUsingConnector.Select(p => $"'{p}'"));
            throw new InvalidOperationException(
                $"Cannot delete connector '{connector.Name}' because it is being used by the following pipeline(s): {pipelineList}. " +
                $"Please remove or update these pipelines before deleting the connector.");
        }

        _logger.LogInformation("Deleting connector {ConnectorId}", id);

        var connectorName = connector.Name;
        var connectorType = connector.Type;

        // Delete associated secrets from Key Vault
        await DeleteSecretsFromKeyVaultAsync(tenantId, id, connector.ConfigJson);

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

        // If config contains Key Vault references (e.g., when testing with edited config),
        // resolve them to get actual secrets before testing the connection
        var configToTest = request.Config;
        var configJson = JsonSerializer.Serialize(configToTest);

        if (_secretResolver.ContainsSecretReferences(configJson))
        {
            _logger.LogDebug("Config contains Key Vault references, resolving secrets for connection test");
            var resolvedConfig = await _secretResolver.ResolveSecretsAsync(configJson);
            configToTest = resolvedConfig;
        }

        // For truly new connections (no keyvault refs), config comes in plain text
        var result = await _connectionTester.TestConnectionAsync(request.Type, request.Provider, configToTest);

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

        // Resolve Key Vault secrets before testing
        var resolvedConfig = await _secretResolver.ResolveSecretsAsync(connector.ConfigJson);
        var result = await _connectionTester.TestConnectionAsync(connector.Type, connector.Provider, resolvedConfig);

        // Update connector with test results
        connector.LastTestedAt = DateTime.UtcNow;
        connector.LastTestResult = result.Success ? TestResults.Success : TestResults.Failed;
        connector.LastTestMessage = result.Message;
        await _context.SaveChangesAsync();

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

        // Resolve Key Vault secrets before schema detection
        var resolvedConfig = await _secretResolver.ResolveSecretsAsync(connector.ConfigJson);
        var result = await _schemaDetector.DetectSchemaAsync(
            connector.Type,
            connector.Provider,
            resolvedConfig,
            request.TableOrResourceName);

        if (result.Success && result.Schema != null)
        {
            // Update connector with detected schema
            connector.SchemaJson = JsonSerializer.Serialize(result.Schema);
            await _context.SaveChangesAsync();
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
        var validTypes = new[] { ConnectorTypes.Database, ConnectorTypes.File, ConnectorTypes.Api, ConnectorTypes.Email };
        if (!validTypes.Contains(type))
        {
            throw new ArgumentException($"Invalid connector type: {type}");
        }

        var validProviders = type switch
        {
            ConnectorTypes.Database => new[] { ConnectorProviders.SqlServer, ConnectorProviders.PostgreSQL, ConnectorProviders.MySQL, ConnectorProviders.Oracle, ConnectorProviders.MongoDb, ConnectorProviders.CosmosDb },
            ConnectorTypes.File => new[] { ConnectorProviders.FTP, ConnectorProviders.SFTP, ConnectorProviders.AzureBlob },
            ConnectorTypes.Api => new[] { ConnectorProviders.REST },
            ConnectorTypes.Email => new[] { ConnectorProviders.Email },
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
        // return !(type == ConnectorTypes.File && provider == ConnectorProviders.Local); // Removed Local
        return true; // All file providers now require credentials
    }

    private ConnectorResponse MapToResponse(Connector connector)
    {
        var config = JsonSerializer.Deserialize<JsonElement>(connector.ConfigJson);

        // Return config with Key Vault references intact (do NOT resolve secrets in API responses)
        // Secrets are only resolved when actually used (connections, pipelines, etc.)
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
            Config = config,
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

    /// <summary>
    /// Stores sensitive fields in Key Vault and returns config with Key Vault references.
    /// </summary>
    private async Task<Dictionary<string, object?>> StoreSecretsInKeyVaultAsync(
        Guid tenantId,
        Guid connectorId,
        JsonElement config)
    {
        var configDict = new Dictionary<string, object?>();

        foreach (var property in config.EnumerateObject())
        {
            var fieldName = property.Name;
            var value = property.Value;

            // Check if this is a sensitive field
            if (EncryptionConstants.SensitiveFields.Contains(fieldName, StringComparer.OrdinalIgnoreCase))
            {
                if (value.ValueKind == JsonValueKind.String)
                {
                    var stringValue = value.GetString();

                    if (!string.IsNullOrWhiteSpace(stringValue))
                    {
                        // Generate secret name and store in Key Vault
                        var secretName = _secretStorageService.GenerateSecretName(tenantId, connectorId, fieldName);
                        await _secretStorageService.StoreSecretAsync(secretName, stringValue);

                        // Replace with Key Vault reference
                        configDict[fieldName] = $"{EncryptionConstants.SecretReferencePrefix}{secretName}";
                        _logger.LogDebug("Stored field {FieldName} in Key Vault as {SecretName}", fieldName, secretName);
                    }
                    else
                    {
                        configDict[fieldName] = stringValue;
                    }
                }
                else
                {
                    configDict[fieldName] = GetJsonValue(value);
                }
            }
            else
            {
                // Not a sensitive field, keep as-is
                configDict[fieldName] = GetJsonValue(value);
            }
        }

        return configDict;
    }

    /// <summary>
    /// Deletes secrets from Key Vault when a connector is deleted.
    /// </summary>
    private async Task DeleteSecretsFromKeyVaultAsync(Guid tenantId, Guid connectorId, string configJson)
    {
        try
        {
            var config = JsonDocument.Parse(configJson).RootElement;

            foreach (var property in config.EnumerateObject())
            {
                if (property.Value.ValueKind == JsonValueKind.String)
                {
                    var stringValue = property.Value.GetString();

                    // Check if this is a Key Vault reference
                    if (!string.IsNullOrWhiteSpace(stringValue) &&
                        stringValue.StartsWith(EncryptionConstants.SecretReferencePrefix, StringComparison.OrdinalIgnoreCase))
                    {
                        var secretName = stringValue.Substring(EncryptionConstants.SecretReferencePrefix.Length);
                        await _secretStorageService.DeleteSecretAsync(secretName);
                        _logger.LogInformation("Deleted Key Vault secret: {SecretName}", secretName);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting secrets from Key Vault for connector {ConnectorId}", connectorId);
            // Don't throw - we still want to delete the connector even if Key Vault cleanup fails
        }
    }

    private static object? GetJsonValue(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.TryGetInt64(out var l) ? l : element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            JsonValueKind.Object => JsonSerializer.Deserialize<Dictionary<string, object>>(element.GetRawText()),
            JsonValueKind.Array => JsonSerializer.Deserialize<List<object>>(element.GetRawText()),
            _ => element.GetRawText()
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

    /// <inheritdoc />
    public string GenerateEmailPreviewHtml(EmailPreviewRequest request)
    {
        return EmailTemplates.GenerateDataExportPreviewHtml(
            request.BodyMessage,
            request.AttachmentFormat,
            request.AttachmentFileName);
    }
}
