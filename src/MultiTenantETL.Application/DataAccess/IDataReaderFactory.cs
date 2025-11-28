using MultiTenantETL.Application.Connectors.DataReaders;
using MultiTenantETL.Domain.Entities;

namespace MultiTenantETL.Application.DataAccess;

/// <summary>
/// Factory for creating data readers based on connector configuration
/// </summary>
public interface IDataReaderFactory
{
    /// <summary>
    /// Creates a data reader for the specified connector
    /// </summary>
    IDataReader CreateReader(Connector connector);
}
