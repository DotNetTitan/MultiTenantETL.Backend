using Microsoft.Extensions.Logging;
using MultiTenantETL.Application.Connectors.DataWriters;
using MultiTenantETL.Domain.Constants;
using MultiTenantETL.Domain.Entities;
using MultiTenantETL.Infrastructure.DataWriters;

namespace MultiTenantETL.Infrastructure.DataAccess.Writers.Api;

/// <summary>
/// Factory for creating API-specific data writers
/// </summary>
public class ApiDataWriterFactory : IApiDataWriterFactory
{
    private readonly RestApiDataWriter _restApiWriter;
    private readonly ILogger<ApiDataWriterFactory> _logger;

    public ApiDataWriterFactory(
        RestApiDataWriter restApiWriter,
        ILogger<ApiDataWriterFactory> logger)
    {
        _restApiWriter = restApiWriter;
        _logger = logger;
    }

    public IDataWriter CreateWriter(Connector connector)
    {
        _logger.LogDebug("Creating API writer for provider {Provider}", connector.Provider);

        return connector.Provider switch
        {
            ConnectorProviders.REST => _restApiWriter,
            _ => throw new NotSupportedException($"API provider '{connector.Provider}' is not supported")
        };
    }
}
