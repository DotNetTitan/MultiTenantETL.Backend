using System.Data;
using System.Data.SqlClient;
using System.Text.Json;
using Microsoft.Extensions.Options;
using MultiTenantETL.Domain.Entities;
using MultiTenantETL.Infrastructure.Configuration;

namespace MultiTenantETL.Infrastructure.DataReaders;

public class SqlServerDataReader : Application.Connectors.DataReaders.IDataReader
{
    private readonly EtlSettings _settings;

    public SqlServerDataReader(IOptions<EtlSettings> settings)
    {
        _settings = settings.Value;
    }

    public string ConnectorType => "SqlServer";

    public async Task<Application.Connectors.DataReaders.DataReadResult> ReadAsync(Connector connector, CancellationToken cancellationToken = default)
    {
        var config = ParseConfig(connector.ConfigJson);
        var result = new Application.Connectors.DataReaders.DataReadResult();

        using var connection = new SqlConnection(config.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        using var command = new SqlCommand(config.Query, connection);
        command.CommandTimeout = config.TimeoutSeconds;

        using var reader = await command.ExecuteReaderAsync(cancellationToken);
        
        // Build schema
        result.Schema = BuildSchema(reader);

        // Read data
        while (await reader.ReadAsync(cancellationToken))
        {
            var row = new Dictionary<string, object?>();
            for (int i = 0; i < reader.FieldCount; i++)
            {
                row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
            }
            result.Rows.Add(row);
        }

        result.TotalRows = result.Rows.Count;
        return result;
    }

    public async IAsyncEnumerable<List<Dictionary<string, object?>>> ReadBatchesAsync(
        Connector connector,
        int batchSize = 0,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var config = ParseConfig(connector.ConfigJson);
        var effectiveBatchSize = batchSize > 0 ? batchSize : _settings.DefaultBatchSize;

        using var connection = new SqlConnection(config.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        using var command = new SqlCommand(config.Query, connection);
        command.CommandTimeout = config.TimeoutSeconds;

        using var reader = await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess, cancellationToken);
        
        var batch = new List<Dictionary<string, object?>>(effectiveBatchSize);

        while (await reader.ReadAsync(cancellationToken))
        {
            var row = new Dictionary<string, object?>();
            for (int i = 0; i < reader.FieldCount; i++)
            {
                row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
            }
            batch.Add(row);

            if (batch.Count >= effectiveBatchSize)
            {
                yield return batch;
                batch = new List<Dictionary<string, object?>>(effectiveBatchSize);
            }
        }

        // Return remaining rows
        if (batch.Count > 0)
        {
            yield return batch;
        }
    }

    public async Task<Application.Connectors.DataReaders.ConnectionTestResult> TestConnectionAsync(Connector connector)
    {
        try
        {
            var config = ParseConfig(connector.ConfigJson);
            using var connection = new SqlConnection(config.ConnectionString);
            await connection.OpenAsync();
            
            return new Application.Connectors.DataReaders.ConnectionTestResult
            {
                IsSuccessful = true,
                Message = "Connection successful"
            };
        }
        catch (Exception ex)
        {
            return new Application.Connectors.DataReaders.ConnectionTestResult
            {
                IsSuccessful = false,
                Message = "Connection failed",
                ErrorDetails = ex.Message
            };
        }
    }

    public async Task<Application.Connectors.DataReaders.SchemaDetectionResult> DetectSchemaAsync(Connector connector)
    {
        try
        {
            var config = ParseConfig(connector.ConfigJson);
            using var connection = new SqlConnection(config.ConnectionString);
            await connection.OpenAsync();

            using var command = new SqlCommand(config.Query, connection);
            command.CommandTimeout = config.TimeoutSeconds;
            
            using var reader = await command.ExecuteReaderAsync(CommandBehavior.SchemaOnly);
            
            return new Application.Connectors.DataReaders.SchemaDetectionResult
            {
                IsSuccessful = true,
                Schema = BuildSchema(reader)
            };
        }
        catch (Exception ex)
        {
            return new Application.Connectors.DataReaders.SchemaDetectionResult
            {
                IsSuccessful = false,
                ErrorMessage = ex.Message
            };
        }
    }

    private SqlServerConfig ParseConfig(string configJson)
    {
        return JsonSerializer.Deserialize<SqlServerConfig>(configJson) 
            ?? throw new InvalidOperationException("Invalid SQL Server configuration");
    }

    private Application.Connectors.DataReaders.SchemaInfo BuildSchema(SqlDataReader reader)
    {
        var schema = new Application.Connectors.DataReaders.SchemaInfo();
        var schemaTable = reader.GetSchemaTable();

        if (schemaTable != null)
        {
            foreach (DataRow row in schemaTable.Rows)
            {
                schema.Fields.Add(new Application.Connectors.DataReaders.FieldDefinition
                {
                    Name = row["ColumnName"].ToString() ?? string.Empty,
                    DataType = row["DataType"].ToString() ?? string.Empty,
                    IsNullable = row["AllowDBNull"] as bool? ?? true,
                    MaxLength = row["ColumnSize"] as int?
                });
            }
        }

        return schema;
    }

    private class SqlServerConfig
    {
        public string ConnectionString { get; set; } = string.Empty;
        public string Query { get; set; } = string.Empty;
        public int TimeoutSeconds { get; set; } = 30;
    }
}
