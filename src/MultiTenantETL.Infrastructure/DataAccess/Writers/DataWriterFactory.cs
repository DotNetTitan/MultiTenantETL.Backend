using Microsoft.Extensions.Logging;
using MultiTenantETL.Application.Connectors.DataWriters;
using MultiTenantETL.Application.DataAccess;
using MultiTenantETL.Domain.Entities;
using MultiTenantETL.Infrastructure.DataAccess.Writers.Api;
using MultiTenantETL.Infrastructure.DataAccess.Writers.Database;
using MultiTenantETL.Infrastructure.DataAccess.Writers.Email;
using MultiTenantETL.Infrastructure.DataAccess.Writers.File;

namespace MultiTenantETL.Infrastructure.DataAccess.Writers;

/// <summary>
/// Main orchestrator for creating data writers based on connector type
/// </summary>
public class DataWriterFactory : IDataWriterFactory
{
    private readonly IDatabaseDataWriterFactory _databaseFactory;
    private readonly IFileDataWriterFactory _fileFactory;
    private readonly IApiDataWriterFactory _apiFactory;
    private readonly IEmailDataWriterFactory _emailFactory;
    private readonly ILogger<DataWriterFactory> _logger;

    public DataWriterFactory(
        IDatabaseDataWriterFactory databaseFactory,
        IFileDataWriterFactory fileFactory,
        IApiDataWriterFactory apiFactory,
        IEmailDataWriterFactory emailFactory,
        ILogger<DataWriterFactory> logger)
    {
        _databaseFactory = databaseFactory;
        _fileFactory = fileFactory;
        _apiFactory = apiFactory;
        _emailFactory = emailFactory;
        _logger = logger;
    }

    public IDataWriter CreateWriter(Connector connector)
    {
        _logger.LogDebug("Creating writer for connector {ConnectorId} of type {Type}",
            connector.Id, connector.Type);

        return connector.Type.ToLower() switch
        {
            "database" => _databaseFactory.CreateWriter(connector),
            "file" => _fileFactory.CreateWriter(connector),
            "api" => _apiFactory.CreateWriter(connector),
            "email" => _emailFactory.CreateWriter(connector),
            _ => throw new NotSupportedException($"Connector type '{connector.Type}' is not supported")
        };
    }
}
