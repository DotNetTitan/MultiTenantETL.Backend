using System.Data;
using System.Data.SqlClient;
using System.Text.Json;
using Microsoft.Extensions.Options;
using MultiTenantETL.Application.Connectors.DataWriters;
using MultiTenantETL.Domain.Entities;
using MultiTenantETL.Infrastructure.Configuration;

namespace MultiTenantETL.Infrastructure.DataWriters;

public class SqlServerDataWriter : IDataWriter
{
    private readonly EtlSettings _settings;

    public SqlServerDataWriter(IOptions<EtlSettings> settings)
    {
        _settings = settings.Value;
    }
    
    public string ConnectorType => "SqlServer";

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

        await using var connection = new SqlConnection(config.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        try
        {
            // Use SqlBulkCopy for optimal performance
            var columns = data[0].Keys.ToList();
            var dataTable = ConvertToDataTable(data, columns);

            using var bulkCopy = new SqlBulkCopy(connection)
            {
                DestinationTableName = config.TableName,
                BatchSize = config.BatchSize > 0 ? config.BatchSize : _settings.DefaultBatchSize,
                BulkCopyTimeout = config.TimeoutSeconds > 0 ? config.TimeoutSeconds : _settings.DefaultTimeoutSeconds,
                EnableStreaming = true
            };

            // Map columns
            foreach (var column in columns)
            {
                bulkCopy.ColumnMappings.Add(column, column);
            }

            await bulkCopy.WriteToServerAsync(dataTable, cancellationToken);
            result.RowsWritten = data.Count;
        }
        catch (Exception ex)
        {
            result.Errors.Add($"Bulk insert failed: {ex.Message}");
            result.RowsFailed = data.Count;
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

        await using var connection = new SqlConnection(config.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        List<string>? columns = null;

        await foreach (var batch in batches.WithCancellation(cancellationToken))
        {
            if (batch.Count == 0) continue;

            try
            {
                // Get columns from first batch
                columns ??= batch[0].Keys.ToList();

                var dataTable = ConvertToDataTable(batch, columns);

                using var bulkCopy = new SqlBulkCopy(connection)
                {
                    DestinationTableName = config.TableName,
                    BatchSize = config.BatchSize > 0 ? config.BatchSize : _settings.DefaultBatchSize,
                    BulkCopyTimeout = config.TimeoutSeconds > 0 ? config.TimeoutSeconds : _settings.DefaultTimeoutSeconds,
                    EnableStreaming = true
                };

                foreach (var column in columns)
                {
                    bulkCopy.ColumnMappings.Add(column, column);
                }

                await bulkCopy.WriteToServerAsync(dataTable, cancellationToken);
                result.RowsWritten += batch.Count;
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Batch write failed: {ex.Message}");
                result.RowsFailed += batch.Count;
            }
        }

        return result;
    }

    private DataTable ConvertToDataTable(List<Dictionary<string, object?>> data, List<string> columns)
    {
        var table = new DataTable();
        
        // Add columns
        foreach (var column in columns)
        {
            table.Columns.Add(column, typeof(object));
        }

        // Add rows
        foreach (var row in data)
        {
            var dataRow = table.NewRow();
            foreach (var column in columns)
            {
                dataRow[column] = row.ContainsKey(column) && row[column] != null 
                    ? row[column] 
                    : DBNull.Value;
            }
            table.Rows.Add(dataRow);
        }

        return table;
    }

    private SqlServerConfig ParseConfig(string configJson)
    {
        return JsonSerializer.Deserialize<SqlServerConfig>(configJson) 
            ?? throw new InvalidOperationException("Invalid SQL Server configuration");
    }

    private class SqlServerConfig
    {
        public string ConnectionString { get; set; } = string.Empty;
        public string TableName { get; set; } = string.Empty;
        public int BatchSize { get; set; } = 0; // 0 = use default from settings
        public int TimeoutSeconds { get; set; } = 0; // 0 = use default from settings
    }
}
