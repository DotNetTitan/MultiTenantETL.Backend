using MultiTenantETL.Application.Connectors.Models;

namespace MultiTenantETL.Application.Connectors;

public interface IConnectorService
{
    Task<ConnectorResponse> CreateAsync(CreateConnectorRequest request, Guid tenantId, Guid userId);
    Task<ConnectorResponse> GetByIdAsync(Guid id, Guid tenantId);
    Task<PagedConnectorResponse> SearchAsync(ConnectorSearchRequest request, Guid tenantId);
    Task<ConnectorResponse> UpdateAsync(Guid id, UpdateConnectorRequest request, Guid tenantId, Guid userId);
    Task DeleteAsync(Guid id, Guid tenantId);
    Task<TestConnectionResponse> TestConnectionAsync(TestConnectionRequest request, Guid tenantId);
    Task<TestConnectionResponse> TestExistingConnectionAsync(Guid id, Guid tenantId);
    Task<DetectSchemaResponse> DetectSchemaAsync(DetectSchemaRequest request, Guid tenantId);
    Task<List<ConnectorListResponse>> GetAllAsync(Guid tenantId);
}
