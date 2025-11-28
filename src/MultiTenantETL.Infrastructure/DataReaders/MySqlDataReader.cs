using System.Text.Json;
using Microsoft.Extensions.Options;
using MultiTenantETL.Application.Connectors.DataReaders;
using MultiTenantETL.Domain.Entities;
using MultiTenantETL.Infrastructure.Configuration;
using MySqlConnector;

namespace MultiTenantETL.Infrastructure.DataReaders;

public class MySqlConnectorDataReader : IDataReader
{
    private readonly EtlSettings _settings;

    public MySqlConnectorDataReader(IOptions<EtlSettings> settings)
    {
        _settings = settings.Value;
    }

    public string ConnectorType => "MySQL";

    public async Task<DataReadResult> ReadAsync(Connector connector, CancellationToken cancellationToken = default)
    {
        var config = ParseConfig(connector.ConfigJson);
        var result = new DataReadResult();

        await using var connection = new MySqlConnection(config.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new MySqlCommand(config.Query, connection);
        command.CommandTimeout = config.TimeoutSeconds;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        
        // Build schema
        result.Schema = BuildSchema(reader);

        // Read data
        while (await reader.ReadAsync(cancellationToken))
        {
            var row = new Dictionary<string, object?>();
            for (int i = 0; i < reader.FieldCount; i++)
            {
                row[reader.GetName(i)] = await reader.IsDBNullAsync(i, cancellationToken) ? null : reader.GetValue(i);
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

        await using var connection = new MySqlConnection(config.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new MySqlCommand(config.Query, connection);
        command.CommandTimeout = config.TimeoutSeconds;

        await using var reader = await command.ExecuteReaderAsync(System.Data.CommandBehavior.SequentialAccess, cancellationToken);
        
        var batch = new List<Dictionary<string, object?>>(effectiveBatchSize);

        while (await reader.ReadAsync(cancellationToken))
        {
            var row = new Dictionary<string, object?>();
            for (int i = 0; i < reader.FieldCount; i++)
            {
                row[reader.GetName(i)] = await reader.IsDBNullAsync(i, cancellationToken) ? null : reader.GetValue(i);
            }
            batch.Add(row);

            if (batch.Count >= effectiveBatchSize)
            {
                yield return batch;
                batch = new List<Dictionary<string, object?>>(effectiveBatchSize);
            }
        }

        if (batch.Count > 0)
        {
            yield return batch;
        }
    }

    public async Task<ConnectionTestResult> TestConnectionAsync(Connector connector)
    {
        try
        {
            var config = ParseConfig(connector.ConfigJson);
            await using var connection = new MySqlConnection(config.ConnectionString);
            await connection.OpenAsync();
            
            return new ConnectionTestResult
            {
                IsSuccessful = true,
                Message = "Connection successful"
            };
        }
        catch (Exception ex)
        {
            return new ConnectionTestResult
            {
                IsSuccessful = false,
                Message = "Connection failed",
                ErrorDetails = ex.Message
            };
        }
    }

    public async Task<SchemaDetectionResult> DetectSchemaAsync(Connector connector)
    {
        try
        {
            var config = ParseConfig(connector.ConfigJson);
            await using var connection = new MySqlConnection(config.ConnectionString);
            await connection.OpenAsync();

            await using var command = new MySqlCommand(config.Query, connection);
            command.CommandTimeout = config.TimeoutSeconds;
            
            await using var reader = await command.ExecuteReaderAsync(System.Data.CommandBehavior.SchemaOnly);
            
            return new SchemaDetectionResult
            {
                IsSuccessful = true,
                Schema = BuildSchema(reader)
            };
        }
        catch (Exception ex)
        {
            return new SchemaDetectionResult
            {
                IsSuccessful = false,
                ErrorMessage = ex.Message
            };
        }
    }

    private MySqlConfig ParseConfig(string configJson)
    {
        return JsonSerializer.Deserialize<MySqlConfig>(configJson) 
            ?? throw new InvalidOperationException("Invalid MySQL configuration");
    }

    private SchemaInfo BuildSchema(MySqlConnector.MySqlDataReader reader)
    {
        var schema = new SchemaInfo();

        for (int i = 0; i < reader.FieldCount; i++)
        {
            schema.Fields.Add(new FieldDefinition
            {
                Name = reader.GetName(i),
                DataType = reader.GetDataTypeName(i),
                IsNullable = true
            });
        }

        return schema;
    }

    private class MySqlConfig
    {
        public string ConnectionString { get; set; } = string.Empty;
        public string Query { get; set; } = string.Empty;
        public int TimeoutSeconds { get; set; } = 30;
    }
}
