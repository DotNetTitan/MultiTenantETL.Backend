using Microsoft.Extensions.Logging;
using MultiTenantETL.Application.Connectors.DataReaders;
using MultiTenantETL.Application.DataAccess;
using MultiTenantETL.Domain.Constants;
using MultiTenantETL.Domain.Entities;
using MultiTenantETL.Infrastructure.DataAccess.Readers.Api;
using MultiTenantETL.Infrastructure.DataAccess.Readers.Database;
using MultiTenantETL.Infrastructure.DataAccess.Readers.File;

namespace MultiTenantETL.Infrastructure.DataAccess.Readers;

/// <summary>
/// Main orchestrator for creating data readers based on connector type
/// </summary>
public class DataReaderFactory : IDataReaderFactory
{
    private readonly IDatabaseDataReaderFactory _databaseFactory;
    private readonly IFileDataReaderFactory _fileFactory;
    private readonly IApiDataReaderFactory _apiFactory;
    private readonly ILogger<DataReaderFactory> _logger;

    public DataReaderFactory(
        IDatabaseDataReaderFactory databaseFactory,
        IFileDataReaderFactory fileFactory,
        IApiDataReaderFactory apiFactory,
        ILogger<DataReaderFactory> logger)
    {
        _databaseFactory = databaseFactory;
        _fileFactory = fileFactory;
        _apiFactory = apiFactory;
        _logger = logger;
    }

    public IDataReader CreateReader(Connector connector)
    {
        _logger.LogDebug("Creating reader for connector {ConnectorId} of type {Type}", 
            connector.Id, connector.Type);

        return connector.Type.ToLower() switch
        {
            "database" => _databaseFactory.CreateReader(connector),
            "file" => _fileFactory.CreateReader(connector),
            "api" => _apiFactory.CreateReader(connector),
            _ => throw new NotSupportedException($"Connector type '{connector.Type}' is not supported")
        };
    }
}
