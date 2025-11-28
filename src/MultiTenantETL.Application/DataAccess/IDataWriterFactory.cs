using MultiTenantETL.Application.Connectors.DataWriters;
using MultiTenantETL.Domain.Entities;

namespace MultiTenantETL.Application.DataAccess;

/// <summary>
/// Factory for creating data writers based on connector configuration
/// </summary>
public interface IDataWriterFactory
{
    /// <summary>
    /// Creates a data writer for the specified connector
    /// </summary>
    IDataWriter CreateWriter(Connector connector);
}
