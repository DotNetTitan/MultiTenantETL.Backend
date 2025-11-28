using Microsoft.Data.SqlClient;
using MySqlConnector;
using Npgsql;

namespace MultiTenantETL.Infrastructure.Services.Database;

public interface IDatabaseConnectionStringBuilder
{
    string BuildSqlServerConnectionString(string host, int port, string database, string username, string password, bool useSsl);
    string BuildPostgreSqlConnectionString(string host, int port, string database, string username, string password, bool useSsl);
    string BuildMySqlConnectionString(string host, int port, string database, string username, string password, bool useSsl);
}

public class DatabaseConnectionStringBuilder : IDatabaseConnectionStringBuilder
{
    public string BuildSqlServerConnectionString(string host, int port, string database, string username, string password, bool useSsl)
    {
        var actualPort = port > 0 ? port : 1433;
        var builder = new SqlConnectionStringBuilder
        {
            DataSource = $"{host},{actualPort}",
            InitialCatalog = database,
            UserID = username,
            Password = password,
            TrustServerCertificate = !useSsl,
            Encrypt = useSsl,
            ConnectTimeout = 30
        };

        return builder.ConnectionString;
    }

    public string BuildPostgreSqlConnectionString(string host, int port, string database, string username, string password, bool useSsl)
    {
        var actualPort = port > 0 ? port : 5432;
        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = host,
            Port = actualPort,
            Database = database,
            Username = username,
            Password = password,
            SslMode = useSsl ? SslMode.Require : SslMode.Prefer,
            Timeout = 30
        };

        return builder.ConnectionString;
    }

    public string BuildMySqlConnectionString(string host, int port, string database, string username, string password, bool useSsl)
    {
        var actualPort = port > 0 ? (uint)port : 3306;
        var builder = new MySqlConnectionStringBuilder
        {
            Server = host,
            Port = actualPort,
            Database = database,
            UserID = username,
            Password = password,
            SslMode = useSsl ? MySqlSslMode.Required : MySqlSslMode.Preferred,
            ConnectionTimeout = 30
        };

        return builder.ConnectionString;
    }
}
