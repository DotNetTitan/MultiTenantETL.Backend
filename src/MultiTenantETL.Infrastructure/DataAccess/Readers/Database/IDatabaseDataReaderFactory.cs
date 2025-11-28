using MultiTenantETL.Application.Connectors.DataReaders;
using MultiTenantETL.Domain.Entities;

namespace MultiTenantETL.Infrastructure.DataAccess.Readers.Database;

/// <summary>
/// Factory for creating database-specific data readers
/// </summary>
public interface IDatabaseDataReaderFactory
{
    /// <summary>
    /// Creates a database reader for the specified provider
    /// </summary>
    IDataReader CreateReader(Connector connector);
}
