using MultiTenantETL.Application.Connectors.DataWriters;
using MultiTenantETL.Domain.Entities;

namespace MultiTenantETL.Infrastructure.DataAccess.Writers.Email;

/// <summary>
/// Factory for creating Email-specific data writers
/// </summary>
public interface IEmailDataWriterFactory
{
    /// <summary>
    /// Creates an Email writer for the specified provider
    /// </summary>
    IDataWriter CreateWriter(Connector connector);
}
