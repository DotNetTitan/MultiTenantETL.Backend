using Microsoft.Extensions.Logging;
using MultiTenantETL.Application.Connectors.DataReaders;
using MultiTenantETL.Domain.Constants;
using MultiTenantETL.Domain.Entities;
using MultiTenantETL.Infrastructure.DataReaders;

namespace MultiTenantETL.Infrastructure.DataAccess.Readers.Database;

/// <summary>
/// Factory for creating database-specific data readers
/// </summary>
public class DatabaseDataReaderFactory : IDatabaseDataReaderFactory
{
    private readonly SqlServerDataReader _sqlServerReader;
    private readonly PostgreSqlDataReader _postgreSqlReader;
    private readonly MySqlDataReader _mySqlReader;
    private readonly ILogger<DatabaseDataReaderFactory> _logger;

    public DatabaseDataReaderFactory(
        SqlServerDataReader sqlServerReader,
        PostgreSqlDataReader postgreSqlReader,
        MySqlDataReader mySqlReader,
        ILogger<DatabaseDataReaderFactory> logger)
    {
        _sqlServerReader = sqlServerReader;
        _postgreSqlReader = postgreSqlReader;
        _mySqlReader = mySqlReader;
        _logger = logger;
    }

    public IDataReader CreateReader(Connector connector)
    {
        _logger.LogDebug("Creating database reader for provider {Provider}", connector.Provider);

        return connector.Provider switch
        {
            ConnectorProviders.SqlServer => _sqlServerReader,
            ConnectorProviders.PostgreSQL => _postgreSqlReader,
            ConnectorProviders.MySQL => _mySqlReader,
            _ => throw new NotSupportedException($"Database provider '{connector.Provider}' is not supported")
        };
    }
}
