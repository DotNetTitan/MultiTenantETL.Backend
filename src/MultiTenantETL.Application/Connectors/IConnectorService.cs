using MultiTenantETL.Application.Connectors.Models;

namespace MultiTenantETL.Application.Connectors;

/// <summary>
/// Service for managing connectors.
/// </summary>
public interface IConnectorService
{
    /// <summary>
    /// Creates a new connector.
    /// </summary>
    Task<ConnectorResponse> CreateAsync(CreateConnectorRequest request, Guid tenantId, Guid userId);

    /// <summary>
    /// Gets a connector by ID.
    /// </summary>
    Task<ConnectorResponse> GetByIdAsync(Guid id, Guid tenantId);

    /// <summary>
    /// Searches for connectors with pagination.
    /// </summary>
    Task<PagedConnectorResponse> SearchAsync(ConnectorSearchRequest request, Guid tenantId);

    /// <summary>
    /// Updates an existing connector.
    /// </summary>
    Task<ConnectorResponse> UpdateAsync(Guid id, UpdateConnectorRequest request, Guid tenantId, Guid userId);

    /// <summary>
    /// Deletes a connector.
    /// </summary>
    Task DeleteAsync(Guid id, Guid tenantId);

    /// <summary>
    /// Tests a connection with the given configuration.
    /// </summary>
    Task<TestConnectionResponse> TestConnectionAsync(TestConnectionRequest request, Guid tenantId);

    /// <summary>
    /// Tests an existing connector by ID.
    /// </summary>
    Task<TestConnectionResponse> TestExistingConnectionAsync(Guid id, Guid tenantId);

    /// <summary>
    /// Detects the schema of a connector.
    /// </summary>
    Task<DetectSchemaResponse> DetectSchemaAsync(DetectSchemaRequest request, Guid tenantId);

    /// <summary>
    /// Detects a preview of the schema.
    /// </summary>
    Task<DetectSchemaResponse> DetectSchemaPreviewAsync(DetectSchemaPreviewRequest request, Guid tenantId);

    /// <summary>
    /// Gets all connectors for a tenant.
    /// </summary>
    Task<List<ConnectorListResponse>> GetAllAsync(Guid tenantId);

    /// <summary>
    /// Generates the HTML preview for a data-export email based on the user's configuration.
    /// Returns the same HTML that would be sent in an actual pipeline execution email.
    /// </summary>
    string GenerateEmailPreviewHtml(EmailPreviewRequest request);
}
