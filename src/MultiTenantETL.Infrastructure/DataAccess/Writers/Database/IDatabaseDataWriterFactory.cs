using MultiTenantETL.Application.Connectors.DataWriters;
using MultiTenantETL.Domain.Entities;

namespace MultiTenantETL.Infrastructure.DataAccess.Writers.Database;

/// <summary>
/// Factory for creating database-specific data writers
/// </summary>
public interface IDatabaseDataWriterFactory
{
    /// <summary>
    /// Creates a database writer for the specified provider
    /// </summary>
    IDataWriter CreateWriter(Connector connector);
}
