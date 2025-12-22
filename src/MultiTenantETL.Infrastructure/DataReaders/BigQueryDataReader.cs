using System.Runtime.CompilerServices;
using System.Text.Json;
using Google.Cloud.BigQuery.V2;
using Google.Apis.Auth.OAuth2;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MultiTenantETL.Application.Connectors.DataReaders;
using MultiTenantETL.Application.Connectors.Models;
using MultiTenantETL.Domain.Entities;
using MultiTenantETL.Infrastructure.Configuration;
using IDataReader = MultiTenantETL.Application.Connectors.DataReaders.IDataReader;

namespace MultiTenantETL.Infrastructure.DataReaders;

public class BigQueryDataReader : IDataReader
{
    private readonly ILogger<BigQueryDataReader> _logger;
    private readonly EtlSettings _settings;

    public BigQueryDataReader(ILogger<BigQueryDataReader> logger, IOptions<EtlSettings> settings)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _settings = settings?.Value ?? throw new ArgumentNullException(nameof(settings));
    }

    public async IAsyncEnumerable<ReadBatch> ReadAsync(
        Connector connector,
        ReadOptions options,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var config = ParseConfig(connector.ConfigJson);
        var client = CreateClient(config);

        var query = config.Query ?? $"SELECT * FROM `{config.ProjectId}.{config.DatasetId}.{config.TableName}`";
        
        var results = await client.ExecuteQueryAsync(query, null, null, null, cancellationToken);

        var batch = new ReadBatch { BatchId = Guid.NewGuid() };
        var rowsRead = 0;

        foreach (var row in results)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var rowData = new Dictionary<string, object?>();
            foreach (var field in row.Schema.Fields)
            {
                rowData[field.Name] = row[field.Name];
            }

            batch.Rows.Add(rowData);
            batch.RowCount++;
            rowsRead++;

            if (batch.RowCount >= options.BatchSize)
            {
                yield return batch;
                batch = new ReadBatch { BatchId = Guid.NewGuid() };
            }

            if (options.MaxRows.HasValue && rowsRead >= options.MaxRows.Value)
            {
                break;
            }
        }

        if (batch.RowCount > 0)
        {
            yield return batch;
        }
    }

    public async Task<bool> TestConnectionAsync(Connector connector, CancellationToken cancellationToken)
    {
        try
        {
            var config = ParseConfig(connector.ConfigJson);
            var client = CreateClient(config);
            await client.GetDatasetAsync(config.DatasetId, null, cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "BigQuery connection test failed");
            return false;
        }
    }

    public async Task<SchemaDetectionResult> DetectSchemaAsync(Connector connector, CancellationToken cancellationToken)
    {
        try
        {
            var config = ParseConfig(connector.ConfigJson);
            var client = CreateClient(config);
            
            if (string.IsNullOrEmpty(config.TableName))
            {
                return new SchemaDetectionResult
                {
                    Success = false,
                    ErrorMessage = "Table name is required for schema detection",
                    DetectedAt = DateTimeOffset.UtcNow
                };
            }

            var table = await client.GetTableAsync(config.DatasetId, config.TableName, null, cancellationToken);
            var fields = table.Schema.Fields.Select(f => new FieldDefinition
            {
                Name = f.Name,
                DataType = f.Type,
                IsNullable = f.Mode != "REQUIRED",
                IsPrimaryKey = false // BigQuery doesn't have PKs in the traditional sense
            }).ToList();

            return new SchemaDetectionResult
            {
                Fields = fields,
                Version = 1,
                DetectedAt = DateTimeOffset.UtcNow,
                Success = true
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Schema detection failed for BigQuery");
            return new SchemaDetectionResult
            {
                Success = false,
                ErrorMessage = ex.Message,
                DetectedAt = DateTimeOffset.UtcNow
            };
        }
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

            return config;
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException("Failed to parse BigQuery connector configuration", ex);
        }
    }

    private class BigQueryConfig
    {
        public string ProjectId { get; set; } = string.Empty;
        public string DatasetId { get; set; } = string.Empty;
        public string? TableName { get; set; }
        public string? Query { get; set; }
        public string? JsonCredentials { get; set; }
    }
}
