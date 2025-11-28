using MultiTenantETL.Application.Connectors.DataReaders;
using MultiTenantETL.Domain.Entities;

namespace MultiTenantETL.Infrastructure.DataAccess.Readers.File;

/// <summary>
/// Factory for creating file-specific data readers
/// </summary>
public interface IFileDataReaderFactory
{
    /// <summary>
    /// Creates a file reader for the specified provider and format
    /// </summary>
    IDataReader CreateReader(Connector connector);
}
