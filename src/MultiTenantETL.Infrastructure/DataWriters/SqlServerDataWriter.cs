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

            // Use upsert if requested and keys are provided
            if (options.UseUpsert && options.UpsertKeys?.Count > 0)
            {
                return await UpsertBatchAsync(connection, config.TableName, batch, options, cancellationToken);
            }

            // Use SqlBulkCopy for bulk insert (fastest for SQL Server)
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

    private async Task<DataWriteResult> UpsertBatchAsync(
        SqlConnection connection,
        string tableName,
        ReadBatch batch,
        WriteOptions options,
        CancellationToken cancellationToken)
    {
        var result = new DataWriteResult { BatchId = batch.BatchId };
        var columns = batch.Rows[0].Keys.ToList();
        var upsertKeys = options.UpsertKeys!;

        // Create temp table and bulk insert into it
        var tempTableName = $"#TempUpsert_{Guid.NewGuid():N}";
        
        // Create temp table with same structure
        var createTempTable = $@"
            SELECT TOP 0 * 
            INTO {tempTableName}
            FROM {tableName}";

        await using (var cmd = new SqlCommand(createTempTable, connection))
        {
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        // Bulk insert into temp table
        var dataTable = ConvertToDataTable(batch);
        using (var bulkCopy = new SqlBulkCopy(connection))
        {
            bulkCopy.DestinationTableName = tempTableName;
            bulkCopy.BulkCopyTimeout = _settings.CommandTimeoutSeconds;
            
            foreach (DataColumn column in dataTable.Columns)
            {
                bulkCopy.ColumnMappings.Add(column.ColumnName, column.ColumnName);
            }
            
            await bulkCopy.WriteToServerAsync(dataTable, cancellationToken);
        }

        // Build MERGE statement
        var joinCondition = string.Join(" AND ", upsertKeys.Select(k => $"target.[{k}] = source.[{k}]"));
        var updateSet = string.Join(", ", columns.Except(upsertKeys).Select(c => $"target.[{c}] = source.[{c}]"));
        var insertColumns = string.Join(", ", columns.Select(c => $"[{c}]"));
        var insertValues = string.Join(", ", columns.Select(c => $"source.[{c}]"));

        var mergeSql = $@"
            MERGE {tableName} AS target
            USING {tempTableName} AS source
            ON {joinCondition}
            WHEN MATCHED THEN
                UPDATE SET {updateSet}
            WHEN NOT MATCHED THEN
                INSERT ({insertColumns})
                VALUES ({insertValues});";

        try
        {
            await using var mergeCmd = new SqlCommand(mergeSql, connection);
            mergeCmd.CommandTimeout = _settings.CommandTimeoutSeconds;
            var rowsAffected = await mergeCmd.ExecuteNonQueryAsync(cancellationToken);
            
            result.RowsWritten = rowsAffected;
            result.RowsFailed = 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MERGE operation failed for batch {BatchId}", batch.BatchId);
            result.RowsFailed = batch.RowCount;
            result.Errors.Add($"MERGE failed: {ex.Message}");
        }
        finally
        {
            // Clean up temp table
            try
            {
                await using var dropCmd = new SqlCommand($"DROP TABLE {tempTableName}", connection);
                await dropCmd.ExecuteNonQueryAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to drop temp table {TempTable}", tempTableName);
            }
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
