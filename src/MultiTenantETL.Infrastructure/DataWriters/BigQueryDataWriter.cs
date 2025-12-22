using System.Text.Json;
using Google.Cloud.BigQuery.V2;
using Google.Apis.Auth.OAuth2;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MultiTenantETL.Application.Connectors.DataWriters;
using MultiTenantETL.Application.Connectors.DataReaders;
using MultiTenantETL.Domain.Entities;
using MultiTenantETL.Infrastructure.Configuration;

namespace MultiTenantETL.Infrastructure.DataWriters;

public class BigQueryDataWriter : IDataWriter
{
    private readonly ILogger<BigQueryDataWriter> _logger;
    private readonly EtlSettings _settings;

    public BigQueryDataWriter(ILogger<BigQueryDataWriter> logger, IOptions<EtlSettings> settings)
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

        if (batch.Rows.Count == 0)
        {
            return result;
        }

        var config = ParseConfig(connector.ConfigJson);
        var client = CreateClient(config);

        try
        {
            if (options.TruncateBeforeLoad)
            {
                await TruncateTableAsync(client, config.DatasetId, config.TableName, cancellationToken);
            }

            var bqRows = batch.Rows.Select(r =>
            {
                var bqRow = new BigQueryInsertRow();
                foreach (var kvp in r)
                {
                    bqRow.Add(kvp.Key, kvp.Value);
                }
                return bqRow;
            });

            await client.InsertRowsAsync(config.DatasetId, config.TableName, bqRows, null, cancellationToken);
            
            result.RowsWritten = batch.RowCount;
            result.RowsFailed = 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write batch to BigQuery");
            result.RowsFailed = batch.RowCount;
            result.Errors.Add(ex.Message);
        }

        return result;
    }

    private async Task TruncateTableAsync(BigQueryClient client, string datasetId, string tableName, CancellationToken cancellationToken)
    {
        var query = $"TRUNCATE TABLE `{client.ProjectId}.{datasetId}.{tableName}`";
        await client.ExecuteQueryAsync(query, null, null, null, cancellationToken);
    }

    private BigQueryClient CreateClient(BigQueryConfig config)
    {
        if (!string.IsNullOrEmpty(config.JsonCredentials))
        {
            var credential = GoogleCredential.FromJson(config.JsonCredentials);
            return BigQueryClient.Create(config.ProjectId, credential);
        }
        
        return BigQueryClient.Create(config.ProjectId);
    }

    private BigQueryConfig ParseConfig(string configJson)
    {
        try
        {
            var config = JsonSerializer.Deserialize<BigQueryConfig>(configJson, JsonSerializerOptionsProvider.Default)
                ?? throw new InvalidOperationException("Invalid BigQuery configuration");

            if (string.IsNullOrEmpty(config.ProjectId))
                throw new InvalidOperationException("BigQuery configuration must include ProjectId");

            if (string.IsNullOrEmpty(config.DatasetId))
                throw new InvalidOperationException("BigQuery configuration must include DatasetId");

            if (string.IsNullOrEmpty(config.TableName))
                throw new InvalidOperationException("BigQuery configuration must include TableName");

            return config;
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException("Failed to parse BigQuery connector configuration", ex);
        }
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }

    private class BigQueryConfig
    {
        public string ProjectId { get; set; } = string.Empty;
        public string DatasetId { get; set; } = string.Empty;
        public string TableName { get; set; } = string.Empty;
        public string? JsonCredentials { get; set; }
    }
}
