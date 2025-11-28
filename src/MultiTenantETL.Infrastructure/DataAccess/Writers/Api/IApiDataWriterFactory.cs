using MultiTenantETL.Application.Connectors.DataWriters;
using MultiTenantETL.Domain.Entities;

namespace MultiTenantETL.Infrastructure.DataAccess.Writers.Api;

/// <summary>
/// Factory for creating API-specific data writers
/// </summary>
public interface IApiDataWriterFactory
{
    /// <summary>
    /// Creates an API writer for the specified provider
    /// </summary>
    IDataWriter CreateWriter(Connector connector);
}
