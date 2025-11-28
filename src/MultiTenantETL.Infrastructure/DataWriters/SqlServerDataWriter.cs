using System.Data;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MultiTenantETL.Application.Connectors.DataReaders;
using MultiTenantETL.Application.Connectors.DataWriters;
using MultiTenantETL.Domain.Entities;
using MultiTenantETL.Infrastructure.Configuration;

namespace MultiTenantETL.Infrastructure.DataWriters;

public class SqlServerDataWriter : IDataWriter
{
    private readonly ILogger<SqlServerDataWriter> _logger;
    private readonly EtlSettings _settings;

    public SqlServerDataWriter(ILogger<SqlServerDataWriter> logger, IOptions<EtlSettings> settings)
    {
        _logger = logger;
        _settings = settings.Value;
    }

    public async Task<DataWriteResult> WriteBatchAsync(
        Connector connector, 
        ReadBatch batch, 
        WriteOptions options,
        CancellationToken cancellationToken)
    {
        var result = new DataWriteResult { BatchId = batch.BatchId };
        
        try
        {
            var config = ParseConfig(connector.ConfigJson);
            await using var connection = new SqlConnection(config.ConnectionString);
            await connection.OpenAsync(cancellationToken);

            if (options.TruncateBeforeLoad)
            {
                await TruncateTableAsync(connection, config.TableName, cancellationToken);
            }

            var dataTable = ConvertToDataTable(batch);
            
            using var bulkCopy = new SqlBulkCopy(connection);
            bulkCopy.DestinationTableName = config.TableName;
            bulkCopy.BatchSize = batch.RowCount;
            bulkCopy.BulkCopyTimeout = _settings.CommandTimeoutSeconds;

            foreach (DataColumn column in dataTable.Columns)
            {
                bulkCopy.ColumnMappings.Add(column.ColumnName, column.ColumnName);
            }

            await bulkCopy.WriteToServerAsync(dataTable, cancellationToken);
            
            result.RowsWritten = batch.RowCount;
            result.RowsFailed = 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write batch to SQL Server");
            result.RowsFailed = batch.RowCount;
            result.Errors.Add(ex.Message);
        }

        return result;
    }

    private async Task TruncateTableAsync(SqlConnection connection, string tableName, CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand($"TRUNCATE TABLE {tableName}", connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private DataTable ConvertToDataTable(ReadBatch batch)
    {
        var dataTable = new DataTable();

        if (batch.Rows.Count == 0)
            return dataTable;

        foreach (var key in batch.Rows[0].Keys)
        {
            dataTable.Columns.Add(key, typeof(object));
        }

        foreach (var row in batch.Rows)
        {
            var dataRow = dataTable.NewRow();
            foreach (var kvp in row)
            {
                dataRow[kvp.Key] = kvp.Value ?? DBNull.Value;
            }
            dataTable.Rows.Add(dataRow);
        }

        return dataTable;
    }

    private SqlServerConfig ParseConfig(string configJson)
    {
        return JsonSerializer.Deserialize<SqlServerConfig>(configJson) 
            ?? throw new InvalidOperationException("Invalid SQL Server configuration");
    }

    public ValueTask DisposeAsync()
    {
        // No resources to dispose for database writer
        return ValueTask.CompletedTask;
    }

    private class SqlServerConfig
    {
        public string ConnectionString { get; set; } = string.Empty;
        public string TableName { get; set; } = string.Empty;
    }
}
