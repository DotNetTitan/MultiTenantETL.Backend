using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MultiTenantETL.Application.Connectors.DataReaders;
using MultiTenantETL.Application.Connectors.DataWriters;
using MultiTenantETL.Domain.Entities;
using MultiTenantETL.Infrastructure.Configuration;
using MySqlConnector;

namespace MultiTenantETL.Infrastructure.DataWriters;

public class MySqlDataWriter : IDataWriter
{
    private readonly ILogger<MySqlDataWriter> _logger;
    private readonly EtlSettings _settings;

    public MySqlDataWriter(ILogger<MySqlDataWriter> logger, IOptions<EtlSettings> settings)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _settings = settings?.Value ?? throw new ArgumentNullException(nameof(settings));
    }

    public async Task<DataWriteResult> WriteBatchAsync(
        Connector connector,
        ReadBatch batch,
        WriteOptions options,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        
        var result = new DataWriteResult { BatchId = batch.BatchId };

        // For empty batches, return success without database operations
        if (batch.Rows.Count == 0)
        {
            return result;
        }

        var config = ParseConfig(connector.ConfigJson);
        
        try
        {
            await using var connection = new MySqlConnection(config.ConnectionString);
            await connection.OpenAsync(cancellationToken);

            // Use upsert if requested
            if (options.UseUpsert && options.UpsertKeys?.Count > 0)
            {
                return await UpsertBatchAsync(connection, config.TableName, batch, options, cancellationToken);
            }

            // Use multi-row INSERT for better performance (bulk insert)
            var columns = batch.Rows[0].Keys.ToList();
            var columnNames = string.Join(", ", columns.Select(c => $"`{c}`"));

            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

            try
            {
                // MySQL supports multi-row INSERT: INSERT INTO table VALUES (row1), (row2), (row3)...
                // Split into chunks to avoid max_allowed_packet limit
                var chunkSize = _settings.MySqlBulkInsertChunkSize;
                for (int i = 0; i < batch.Rows.Count; i += chunkSize)
                {
                    var chunk = batch.Rows.Skip(i).Take(chunkSize).ToList();
                    var valuePlaceholders = new List<string>();
                    var parameters = new List<MySqlParameter>();

                    for (int rowIdx = 0; rowIdx < chunk.Count; rowIdx++)
                    {
                        var rowPlaceholders = new List<string>();
                        for (int colIdx = 0; colIdx < columns.Count; colIdx++)
                        {
                            var paramName = $"@p{rowIdx}_{colIdx}";
                            rowPlaceholders.Add(paramName);
                            var value = chunk[rowIdx][columns[colIdx]];
                            parameters.Add(new MySqlParameter(paramName, value ?? DBNull.Value));
                        }
                        valuePlaceholders.Add($"({string.Join(", ", rowPlaceholders)})");
                    }

                    var insertQuery = $"INSERT INTO `{config.TableName}` ({columnNames}) VALUES {string.Join(", ", valuePlaceholders)}";

                    await using var command = new MySqlCommand(insertQuery, connection, transaction);
                    command.Parameters.AddRange(parameters.ToArray());
                    await command.ExecuteNonQueryAsync(cancellationToken);
                }

                await transaction.CommitAsync(cancellationToken);
                result.RowsWritten = batch.RowCount;
                result.RowsFailed = 0;
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write batch to MySQL");
            result.RowsFailed = batch.RowCount;
            result.Errors.Add(ex.Message);
        }

        return result;
    }

    private async Task<DataWriteResult> UpsertBatchAsync(
        MySqlConnection connection,
        string tableName,
        ReadBatch batch,
        WriteOptions options,
        CancellationToken cancellationToken)
    {
        var result = new DataWriteResult { BatchId = batch.BatchId };
        var columns = batch.Rows[0].Keys.ToList();
        var upsertKeys = options.UpsertKeys!;

        // Columns to update (exclude upsert keys to avoid updating them)
        var updateColumns = columns.Except(upsertKeys).ToList();
        var columnNames = string.Join(", ", columns.Select(c => $"`{c}`"));
        var updateSet = string.Join(", ", updateColumns.Select(c => $"`{c}` = VALUES(`{c}`)"));

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            // Split into chunks to avoid max_allowed_packet limit
            var chunkSize = _settings.MySqlBulkInsertChunkSize;
            for (int i = 0; i < batch.Rows.Count; i += chunkSize)
            {
                var chunk = batch.Rows.Skip(i).Take(chunkSize).ToList();
                var valuePlaceholders = new List<string>();
                var parameters = new List<MySqlParameter>();

                for (int rowIdx = 0; rowIdx < chunk.Count; rowIdx++)
                {
                    var rowPlaceholders = new List<string>();
                    for (int colIdx = 0; colIdx < columns.Count; colIdx++)
                    {
                        var paramName = $"@p{rowIdx}_{colIdx}";
                        rowPlaceholders.Add(paramName);
                        var value = chunk[rowIdx][columns[colIdx]];
                        parameters.Add(new MySqlParameter(paramName, value ?? DBNull.Value));
                    }
                    valuePlaceholders.Add($"({string.Join(", ", rowPlaceholders)})");
                }

                // MySQL ON DUPLICATE KEY UPDATE
                var upsertQuery = $@"
                    INSERT INTO `{tableName}` ({columnNames})
                    VALUES {string.Join(", ", valuePlaceholders)}
                    ON DUPLICATE KEY UPDATE {updateSet}";

                await using var command = new MySqlCommand(upsertQuery, connection, transaction);
                command.Parameters.AddRange(parameters.ToArray());
                await command.ExecuteNonQueryAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
            result.RowsWritten = batch.RowCount;
            result.RowsFailed = 0;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "Upsert operation failed for batch {BatchId}", batch.BatchId);
            result.RowsFailed = batch.RowCount;
            result.Errors.Add($"Upsert failed: {ex.Message}");
        }

        return result;
    }

    private async Task TruncateTableAsync(MySqlConnection connection, string tableName, CancellationToken cancellationToken)
    {
        await using var command = new MySqlCommand($"TRUNCATE TABLE `{tableName}`", connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private MySqlConfig ParseConfig(string configJson)
    {
        MySqlConfig config;
        try
        {
            config = JsonSerializer.Deserialize<MySqlConfig>(configJson)
                ?? throw new InvalidOperationException("Invalid MySQL configuration");
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException("Failed to parse MySQL connector configuration", ex);
        }

        // Validate required fields
        if (string.IsNullOrEmpty(config.ConnectionString))
        {
            throw new InvalidOperationException("MySQL configuration must include ConnectionString");
        }

        if (string.IsNullOrEmpty(config.TableName))
        {
            throw new InvalidOperationException("MySQL configuration must include TableName");
        }

        return config;
    }

    public ValueTask DisposeAsync()
    {
        // No resources to dispose for database writer
        return ValueTask.CompletedTask;
    }

    private class MySqlConfig
    {
        public string ConnectionString { get; set; } = string.Empty;
        public string TableName { get; set; } = string.Empty;
    }
}
