using Microsoft.Extensions.Logging;
using MultiTenantETL.Application.Connectors.DataWriters;
using MultiTenantETL.Domain.Constants;
using MultiTenantETL.Domain.Entities;
using MultiTenantETL.Infrastructure.DataWriters;

namespace MultiTenantETL.Infrastructure.DataAccess.Writers.Database;

/// <summary>
/// Factory for creating database-specific data writers
/// </summary>
public class DatabaseDataWriterFactory : IDatabaseDataWriterFactory
{
    private readonly SqlServerDataWriter _sqlServerWriter;
    private readonly PostgreSqlDataWriter _postgreSqlWriter;
    private readonly MySqlDataWriter _mySqlWriter;
    private readonly ILogger<DatabaseDataWriterFactory> _logger;

    public DatabaseDataWriterFactory(
        SqlServerDataWriter sqlServerWriter,
        PostgreSqlDataWriter postgreSqlWriter,
        MySqlDataWriter mySqlWriter,
        ILogger<DatabaseDataWriterFactory> logger)
    {
        _sqlServerWriter = sqlServerWriter;
        _postgreSqlWriter = postgreSqlWriter;
        _mySqlWriter = mySqlWriter;
        _logger = logger;
    }

    public IDataWriter CreateWriter(Connector connector)
    {
        _logger.LogDebug("Creating database writer for provider {Provider}", connector.Provider);

        return connector.Provider switch
        {
            ConnectorProviders.SqlServer => _sqlServerWriter,
            ConnectorProviders.PostgreSQL => _postgreSqlWriter,
            ConnectorProviders.MySQL => _mySqlWriter,
            _ => throw new NotSupportedException($"Database provider '{connector.Provider}' is not supported")
        };
    }
}
