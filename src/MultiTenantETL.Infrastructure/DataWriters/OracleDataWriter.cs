using System.Data;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MultiTenantETL.Application.Connectors.DataReaders;
using MultiTenantETL.Application.Connectors.DataWriters;
using MultiTenantETL.Domain.Entities;
using MultiTenantETL.Infrastructure.Configuration;
using Oracle.ManagedDataAccess.Client;

namespace MultiTenantETL.Infrastructure.DataWriters;

public class OracleDataWriter : IDataWriter
{
    private readonly ILogger<OracleDataWriter> _logger;
    private readonly EtlSettings _settings;

    public OracleDataWriter(ILogger<OracleDataWriter> logger, IOptions<EtlSettings> settings)
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
            await using var connection = new OracleConnection(config.ConnectionString);
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

            // Use bulk insert with array binding for better performance
            await BulkInsertAsync(connection, config.TableName, batch, cancellationToken);

            result.RowsWritten = batch.RowCount;
            result.RowsFailed = 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write batch to Oracle");
            result.RowsFailed = batch.RowCount;
            result.Errors.Add(ex.Message);
        }

        return result;
    }

    private async Task BulkInsertAsync(
        OracleConnection connection,
        string tableName,
        ReadBatch batch,
        CancellationToken cancellationToken)
    {
        if (batch.Rows.Count == 0)
            return;

        var columns = batch.Rows[0].Keys.ToList();
        var columnList = string.Join(", ", columns.Select(c => $"\"{c}\""));
        var parameterList = string.Join(", ", columns.Select(c => $":{c}"));

        var insertSql = $"INSERT INTO {tableName} ({columnList}) VALUES ({parameterList})";

        await using var command = new OracleCommand(insertSql, connection);
        command.CommandTimeout = _settings.CommandTimeoutSeconds;
        command.ArrayBindCount = batch.RowCount;

        // Prepare parameter arrays
        foreach (var column in columns)
        {
            var values = batch.Rows.Select(row => row[column]).ToArray();
            command.Parameters.Add(new OracleParameter($":{column}", values));
        }

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task<DataWriteResult> UpsertBatchAsync(
        OracleConnection connection,
        string tableName,
        ReadBatch batch,
        WriteOptions options,
        CancellationToken cancellationToken)
    {
        var result = new DataWriteResult { BatchId = batch.BatchId };
        var columns = batch.Rows[0].Keys.ToList();
        var upsertKeys = options.UpsertKeys!;

        // Create temp table and insert into it
        var tempTableName = $"TEMP_UPSERT_{Guid.NewGuid():N}";

        // Create temp table with same structure
        var createTempTable = $"CREATE GLOBAL TEMPORARY TABLE {tempTableName} AS SELECT * FROM {tableName} WHERE 1=0";

        await using (var cmd = new OracleCommand(createTempTable, connection))
        {
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        // Insert into temp table
        await BulkInsertAsync(connection, tempTableName, batch, cancellationToken);

        // Build MERGE statement
        var joinCondition = string.Join(" AND ", upsertKeys.Select(k => $"target.\"{k}\" = source.\"{k}\""));
        var updateSet = string.Join(", ", columns.Except(upsertKeys).Select(c => $"target.\"{c}\" = source.\"{c}\""));
        var insertColumns = string.Join(", ", columns.Select(c => $"\"{c}\""));
        var insertValues = string.Join(", ", columns.Select(c => $"source.\"{c}\""));

        var mergeSql = $@"
            MERGE INTO {tableName} target
            USING {tempTableName} source
            ON ({joinCondition})
            WHEN MATCHED THEN
                UPDATE SET {updateSet}
            WHEN NOT MATCHED THEN
                INSERT ({insertColumns})
                VALUES ({insertValues})";

        try
        {
            await using var mergeCmd = new OracleCommand(mergeSql, connection);
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
                await using var dropCmd = new OracleCommand($"DROP TABLE {tempTableName}", connection);
                await dropCmd.ExecuteNonQueryAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to drop temp table {TempTable}", tempTableName);
            }
        }

        return result;
    }

    private async Task TruncateTableAsync(OracleConnection connection, string tableName, CancellationToken cancellationToken)
    {
        await using var command = new OracleCommand($"TRUNCATE TABLE {tableName}", connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private OracleConfig ParseConfig(string configJson)
    {
        return JsonSerializer.Deserialize<OracleConfig>(configJson)
            ?? throw new InvalidOperationException("Invalid Oracle configuration");
    }

    public ValueTask DisposeAsync()
    {
        // No resources to dispose for database writer
        return ValueTask.CompletedTask;
    }

    private class OracleConfig
    {
        public string ConnectionString { get; set; } = string.Empty;
        public string TableName { get; set; } = string.Empty;
    }
}