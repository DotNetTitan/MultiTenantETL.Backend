using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using MultiTenantETL.Application.Connectors.DataReaders;
using MultiTenantETL.Application.Connectors.DataWriters;
using MultiTenantETL.Domain.Entities;
using Npgsql;

namespace MultiTenantETL.Infrastructure.DataWriters;

public class PostgreSqlDataWriter : IDataWriter
{
    private readonly ILogger<PostgreSqlDataWriter> _logger;

    public PostgreSqlDataWriter(ILogger<PostgreSqlDataWriter> logger)
    {
        _logger = logger;
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
            await using var connection = new NpgsqlConnection(config.ConnectionString);
            await connection.OpenAsync(cancellationToken);

            if (options.TruncateBeforeLoad)
            {
                await TruncateTableAsync(connection, config.TableName, cancellationToken);
            }

            if (batch.Rows.Count == 0)
                return result;

            var columns = batch.Rows[0].Keys.ToList();
            var copyCommand = $"COPY {config.TableName} ({string.Join(", ", columns)}) FROM STDIN (FORMAT BINARY)";

            await using var writer = await connection.BeginBinaryImportAsync(copyCommand, cancellationToken);

            foreach (var row in batch.Rows)
            {
                await writer.StartRowAsync(cancellationToken);
                
                foreach (var column in columns)
                {
                    var value = row[column];
                    if (value == null)
                    {
                        await writer.WriteNullAsync(cancellationToken);
                    }
                    else
                    {
                        await writer.WriteAsync(value, cancellationToken);
                    }
                }
            }

            await writer.CompleteAsync(cancellationToken);
            
            result.RowsWritten = batch.RowCount;
            result.RowsFailed = 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write batch to PostgreSQL");
            result.RowsFailed = batch.RowCount;
            result.Errors.Add(ex.Message);
        }

        return result;
    }

    private async Task TruncateTableAsync(NpgsqlConnection connection, string tableName, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand($"TRUNCATE TABLE {tableName}", connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private PostgreSqlConfig ParseConfig(string configJson)
    {
        return JsonSerializer.Deserialize<PostgreSqlConfig>(configJson) 
            ?? throw new InvalidOperationException("Invalid PostgreSQL configuration");
    }

    public ValueTask DisposeAsync()
    {
        // No resources to dispose for database writer
        return ValueTask.CompletedTask;
    }

    private class PostgreSqlConfig
    {
        public string ConnectionString { get; set; } = string.Empty;
        public string TableName { get; set; } = string.Empty;
    }
}
