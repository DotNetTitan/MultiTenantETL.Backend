using System.Text.Json;
using Microsoft.Extensions.Options;
using MultiTenantETL.Application.Connectors.DataWriters;
using MultiTenantETL.Domain.Entities;
using MultiTenantETL.Infrastructure.Configuration;
using Npgsql;

namespace MultiTenantETL.Infrastructure.DataWriters;

public class PostgreSqlDataWriter : IDataWriter
{
    private readonly EtlSettings _settings;

    public PostgreSqlDataWriter(IOptions<EtlSettings> settings)
    {
        _settings = settings.Value;
    }
    
    public string ConnectorType => "PostgreSQL";

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

        await using var connection = new NpgsqlConnection(config.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        try
        {
            var columns = data[0].Keys.ToList();
            var columnList = string.Join(", ", columns.Select(c => $"\"{c}\""));
            
            // Use COPY for bulk insert - PostgreSQL's fastest method
            var copyCommand = $"COPY \"{config.TableName}\" ({columnList}) FROM STDIN (FORMAT BINARY)";
            
            await using var writer = await connection.BeginBinaryImportAsync(copyCommand, cancellationToken);
            
            foreach (var row in data)
            {
                try
                {
                    await writer.StartRowAsync(cancellationToken);
                    
                    foreach (var column in columns)
                    {
                        var value = row.ContainsKey(column) ? row[column] : null;
                        
                        if (value == null)
                        {
                            await writer.WriteNullAsync(cancellationToken);
                        }
                        else
                        {
                            await writer.WriteAsync(value, cancellationToken);
                        }
                    }
                    
                    result.RowsWritten++;
                }
                catch (Exception ex)
                {
                    result.RowsFailed++;
                    result.Errors.Add($"Row {result.RowsWritten + result.RowsFailed}: {ex.Message}");
                }
            }

            await writer.CompleteAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            result.Errors.Add($"Bulk insert failed: {ex.Message}");
            result.RowsFailed = data.Count - result.RowsWritten;
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

        await using var connection = new NpgsqlConnection(config.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        List<string>? columns = null;
        string? copyCommand = null;

        await foreach (var batch in batches.WithCancellation(cancellationToken))
        {
            if (batch.Count == 0) continue;

            try
            {
                // Get columns from first batch
                if (columns == null)
                {
                    columns = batch[0].Keys.ToList();
                    var columnList = string.Join(", ", columns.Select(c => $"\"{c}\""));
                    copyCommand = $"COPY \"{config.TableName}\" ({columnList}) FROM STDIN (FORMAT BINARY)";
                }

                await using var writer = await connection.BeginBinaryImportAsync(copyCommand!, cancellationToken);
                
                foreach (var row in batch)
                {
                    try
                    {
                        await writer.StartRowAsync(cancellationToken);
                        
                        foreach (var column in columns)
                        {
                            var value = row.ContainsKey(column) ? row[column] : null;
                            
                            if (value == null)
                            {
                                await writer.WriteNullAsync(cancellationToken);
                            }
                            else
                            {
                                await writer.WriteAsync(value, cancellationToken);
                            }
                        }
                        
                        result.RowsWritten++;
                    }
                    catch (Exception ex)
                    {
                        result.RowsFailed++;
                        result.Errors.Add($"Row write failed: {ex.Message}");
                    }
                }

                await writer.CompleteAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Batch write failed: {ex.Message}");
                result.RowsFailed += batch.Count - (batch.Count - result.RowsFailed);
            }
        }

        return result;
    }

    private PostgreSqlConfig ParseConfig(string configJson)
    {
        return JsonSerializer.Deserialize<PostgreSqlConfig>(configJson) 
            ?? throw new InvalidOperationException("Invalid PostgreSQL configuration");
    }

    private class PostgreSqlConfig
    {
        public string ConnectionString { get; set; } = string.Empty;
        public string TableName { get; set; } = string.Empty;
        public int BatchSize { get; set; } = 0;
        public int TimeoutSeconds { get; set; } = 0;
    }
}
