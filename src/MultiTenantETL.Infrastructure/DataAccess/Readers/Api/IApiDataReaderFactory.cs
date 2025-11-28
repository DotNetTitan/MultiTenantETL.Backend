using MultiTenantETL.Application.Connectors.DataReaders;
using MultiTenantETL.Domain.Entities;

namespace MultiTenantETL.Infrastructure.DataAccess.Readers.Api;

/// <summary>
/// Factory for creating API-specific data readers
/// </summary>
public interface IApiDataReaderFactory
{
    /// <summary>
    /// Creates an API reader for the specified provider
    /// </summary>
    IDataReader CreateReader(Connector connector);
}
