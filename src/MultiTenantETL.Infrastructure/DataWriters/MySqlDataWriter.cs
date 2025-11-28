using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using MultiTenantETL.Application.Connectors.DataWriters;
using MultiTenantETL.Domain.Entities;
using MultiTenantETL.Infrastructure.Configuration;
using MySqlConnector;

namespace MultiTenantETL.Infrastructure.DataWriters;

public class MySqlConnectorDataWriter : IDataWriter
{
    private readonly EtlSettings _settings;

    public MySqlConnectorDataWriter(IOptions<EtlSettings> settings)
    {
        _settings = settings.Value;
    }
    
    public string ConnectorType => "MySQL";

    public async Task<DataWriteResult> WriteAsync(
        Connector connector,
        List<Dictionary<string, object?>> data,
        CancellationToken cancellationToken = default)
    {
        var config = ParseConfig(connector.ConfigJson);
        var result = new DataWriteResult();

        if (data.Count == 0)
        {
            result.Warnings.Add("No data to write");
            return result;
        }

        await using var connection = new MySqlConnection(config.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        try
        {
            var columns = data[0].Keys.ToList();
            var effectiveBatchSize = config.BatchSize > 0 ? config.BatchSize : _settings.MySqlBatchSize;
            
            // Process in batches to avoid parameter limit
            for (int i = 0; i < data.Count; i += effectiveBatchSize)
            {
                var batch = data.Skip(i).Take(effectiveBatchSize).ToList();
                
                await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
                
                try
                {
                    // Build multi-row INSERT statement
                    var insertSql = BuildBatchInsertStatement(config.TableName, columns, batch.Count);
                    
                    await using var command = new MySqlCommand(insertSql, connection, transaction);
                    command.CommandTimeout = config.TimeoutSeconds > 0 ? config.TimeoutSeconds : _settings.DefaultTimeoutSeconds;
                    
                    // Add parameters for all rows in batch
                    int paramIndex = 0;
                    foreach (var row in batch)
                    {
                        foreach (var column in columns)
                        {
                            var value = row.ContainsKey(column) ? row[column] : null;
                            command.Parameters.AddWithValue($"@p{paramIndex++}", value ?? DBNull.Value);
                        }
                    }

                    await command.ExecuteNonQueryAsync(cancellationToken);
                    await transaction.CommitAsync(cancellationToken);
                    
                    result.RowsWritten += batch.Count;
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    result.RowsFailed += batch.Count;
                    result.Errors.Add($"Batch starting at row {i}: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            result.Errors.Add($"Bulk insert failed: {ex.Message}");
        }

        return result;
    }

    public async Task<DataWriteResult> WriteBatchesAsync(
        Connector connector,
        IAsyncEnumerable<List<Dictionary<string, object?>>> batches,
        CancellationToken cancellationToken = default)
    {
        var config = ParseConfig(connector.ConfigJson);
        var result = new DataWriteResult();

        await using var connection = new MySqlConnection(config.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        List<string>? columns = null;

        await foreach (var batch in batches.WithCancellation(cancellationToken))
        {
            if (batch.Count == 0) continue;

            try
            {
                // Get columns from first batch
                columns ??= batch[0].Keys.ToList();

                await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
                
                try
                {
                    var insertSql = BuildBatchInsertStatement(config.TableName, columns, batch.Count);
                    
                    await using var command = new MySqlCommand(insertSql, connection, transaction);
                    command.CommandTimeout = config.TimeoutSeconds > 0 ? config.TimeoutSeconds : _settings.DefaultTimeoutSeconds;
                    
                    int paramIndex = 0;
                    foreach (var row in batch)
                    {
                        foreach (var column in columns)
                        {
                            var value = row.ContainsKey(column) ? row[column] : null;
                            command.Parameters.AddWithValue($"@p{paramIndex++}", value ?? DBNull.Value);
                        }
                    }

                    await command.ExecuteNonQueryAsync(cancellationToken);
                    await transaction.CommitAsync(cancellationToken);
                    
                    result.RowsWritten += batch.Count;
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    result.RowsFailed += batch.Count;
                    result.Errors.Add($"Batch write failed: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Batch processing failed: {ex.Message}");
            }
        }

        return result;
    }

    private string BuildBatchInsertStatement(string tableName, List<string> columns, int rowCount)
    {
        var columnList = string.Join(", ", columns.Select(c => $"`{c}`"));
        
        var valueSets = new List<string>();
        int paramIndex = 0;
        
        for (int i = 0; i < rowCount; i++)
        {
            var paramList = string.Join(", ", columns.Select(_ => $"@p{paramIndex++}"));
            valueSets.Add($"({paramList})");
        }
        
        return $"INSERT INTO `{tableName}` ({columnList}) VALUES {string.Join(", ", valueSets)}";
    }

    private MySqlConfig ParseConfig(string configJson)
    {
        return JsonSerializer.Deserialize<MySqlConfig>(configJson) 
            ?? throw new InvalidOperationException("Invalid MySQL configuration");
    }

    private class MySqlConfig
    {
        public string ConnectionString { get; set; } = string.Empty;
        public string TableName { get; set; } = string.Empty;
        public int BatchSize { get; set; } = 0;
        public int TimeoutSeconds { get; set; } = 0;
    }
}
