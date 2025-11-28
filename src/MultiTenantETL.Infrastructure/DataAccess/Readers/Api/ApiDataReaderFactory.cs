using Microsoft.Extensions.Logging;
using MultiTenantETL.Application.Connectors.DataReaders;
using MultiTenantETL.Domain.Constants;
using MultiTenantETL.Domain.Entities;
using MultiTenantETL.Infrastructure.DataReaders;

namespace MultiTenantETL.Infrastructure.DataAccess.Readers.Api;

/// <summary>
/// Factory for creating API-specific data readers
/// </summary>
public class ApiDataReaderFactory : IApiDataReaderFactory
{
    private readonly RestApiDataReader _restApiReader;
    private readonly ILogger<ApiDataReaderFactory> _logger;

    public ApiDataReaderFactory(
        RestApiDataReader restApiReader,
        ILogger<ApiDataReaderFactory> logger)
    {
        _restApiReader = restApiReader;
        _logger = logger;
    }

    public IDataReader CreateReader(Connector connector)
    {
        _logger.LogDebug("Creating API reader for provider {Provider}", connector.Provider);

        return connector.Provider switch
        {
            ConnectorProviders.REST => _restApiReader,
            _ => throw new NotSupportedException($"API provider '{connector.Provider}' is not supported")
        };
    }
}
