using Microsoft.Data.SqlClient;
using MySqlConnector;
using Npgsql;
using Oracle.ManagedDataAccess.Client;

namespace MultiTenantETL.Infrastructure.Services.Database;

public interface IDatabaseConnectionStringBuilder
{
    string BuildSqlServerConnectionString(string host, int port, string database, string username, string password, bool useSsl);
    string BuildPostgreSqlConnectionString(string host, int port, string database, string username, string password, bool useSsl);
    string BuildMySqlConnectionString(string host, int port, string database, string username, string password, bool useSsl);
    string BuildOracleConnectionString(string host, int port, string database, string username, string password, bool useSsl);
    string BuildMongoDbConnectionString(string host, int port, string database, string username, string password, bool useSsl, string? additionalParams = null);
    string BuildCosmosDbConnectionString(string endpoint, string key);
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

    public string BuildOracleConnectionString(string host, int port, string database, string username, string password, bool useSsl)
    {
        var actualPort = port > 0 ? port : 1521;
        var dataSource = $"(DESCRIPTION=(ADDRESS=(PROTOCOL=TCP)(HOST={host})(PORT={actualPort}))(CONNECT_DATA=(SERVICE_NAME={database})))";

        var builder = new OracleConnectionStringBuilder
        {
            DataSource = dataSource,
            UserID = username,
            Password = password
        };

        return builder.ConnectionString;
    }

    public string BuildMongoDbConnectionString(string host, int port, string database, string username, string password, bool useSsl, string? additionalParams = null)
    {
        var actualPort = port > 0 ? port : 27017;
        var auth = !string.IsNullOrEmpty(username) ? $"{username}:{password}@" : "";
        var ssl = useSsl ? "?ssl=true" : "";
        
        if (!string.IsNullOrEmpty(additionalParams))
        {
            ssl += string.IsNullOrEmpty(ssl) ? $"?{additionalParams}" : $"&{additionalParams}";
        }

        return $"mongodb://{auth}{host}:{actualPort}/{database}{ssl}";
    }

    public string BuildCosmosDbConnectionString(string endpoint, string key)
    {
        return $"AccountEndpoint={endpoint};AccountKey={key};";
    }
}
