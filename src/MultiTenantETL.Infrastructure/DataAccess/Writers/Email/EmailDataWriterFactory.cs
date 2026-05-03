using Microsoft.Extensions.Logging;
using MultiTenantETL.Application.Connectors.DataWriters;
using MultiTenantETL.Domain.Constants;
using MultiTenantETL.Domain.Entities;
using MultiTenantETL.Infrastructure.DataWriters;

namespace MultiTenantETL.Infrastructure.DataAccess.Writers.Email;

/// <summary>
/// Factory for creating Email-specific data writers
/// </summary>
public class EmailDataWriterFactory : IEmailDataWriterFactory
{
    private readonly EmailDataWriter _emailWriter;
    private readonly ILogger<EmailDataWriterFactory> _logger;

    public EmailDataWriterFactory(
        EmailDataWriter emailWriter,
        ILogger<EmailDataWriterFactory> logger)
    {
        _emailWriter = emailWriter;
        _logger = logger;
    }

    /// <summary>
    /// Creates an Email writer for the specified provider
    /// </summary>
    public IDataWriter CreateWriter(Connector connector)
    {
        _logger.LogDebug("Creating Email writer for provider {Provider}", connector.Provider);

        return connector.Provider switch
        {
            ConnectorProviders.Email => _emailWriter,
            _ => throw new NotSupportedException($"Email provider '{connector.Provider}' is not supported")
        };
    }
}
