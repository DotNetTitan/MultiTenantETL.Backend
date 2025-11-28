using MultiTenantETL.Application.Connectors.DataWriters;
using MultiTenantETL.Domain.Entities;

namespace MultiTenantETL.Infrastructure.DataAccess.Writers.File;

/// <summary>
/// Factory for creating file-specific data writers
/// </summary>
public interface IFileDataWriterFactory
{
    /// <summary>
    /// Creates a file writer for the specified provider and format
    /// </summary>
    IDataWriter CreateWriter(Connector connector);
}
