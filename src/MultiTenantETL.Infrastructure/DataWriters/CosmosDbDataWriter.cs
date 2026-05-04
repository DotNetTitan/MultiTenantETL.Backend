using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using MultiTenantETL.Application.Common.Interfaces;
using MultiTenantETL.Application.Connectors.DataReaders;
using MultiTenantETL.Application.Connectors.DataWriters;
using MultiTenantETL.Domain.Entities;
using MultiTenantETL.Infrastructure.Configuration;
using System.Text.Json;

namespace MultiTenantETL.Infrastructure.DataWriters;

public class CosmosDbDataWriter : IDataWriter
{
    private readonly ILogger<CosmosDbDataWriter> _logger;
    private readonly ISecretResolver _secretResolver;

    public CosmosDbDataWriter(ILogger<CosmosDbDataWriter> logger, ISecretResolver secretResolver)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _secretResolver = secretResolver ?? throw new ArgumentNullException(nameof(secretResolver));
    }

    public async Task<DataWriteResult> WriteBatchAsync(
        Connector connector,
        ReadBatch batch,
        WriteOptions options,
        CancellationToken cancellationToken)
    {
        var result = new DataWriteResult { BatchId = batch.BatchId };
        var config = ParseConfig(connector.ConfigJson);

        using var client = new CosmosClient(config.Endpoint, config.Key, new CosmosClientOptions
        {
            AllowBulkExecution = true
        });

        var container = client.GetContainer(config.Database, config.Container);
        var tasks = new List<Task>();

        foreach (var row in batch.Rows)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Ensure ID exists for Cosmos DB
            if (!row.ContainsKey("id"))
            {
                row["id"] = Guid.NewGuid().ToString();
            }

            if (options.UseUpsert)
            {
                tasks.Add(container.UpsertItemAsync<Dictionary<string, object?>>(row, cancellationToken: cancellationToken)
                    .ContinueWith(t =>
                    {
                        if (t.IsFaulted)
                        {
                            result.RowsFailed++;
                            result.Errors.Add(t.Exception?.Flatten().InnerException?.Message ?? "Upsert failed");
                        }
                        else result.RowsWritten++;
                    }));
            }
            else
            {
                tasks.Add(container.CreateItemAsync<Dictionary<string, object?>>(row, cancellationToken: cancellationToken)
                    .ContinueWith(t =>
                    {
                        if (t.IsFaulted)
                        {
                            result.RowsFailed++;
                            result.Errors.Add(t.Exception?.Flatten().InnerException?.Message ?? "Create failed");
                        }
                        else result.RowsWritten++;
                    }));
            }
        }

        await Task.WhenAll(tasks);
        return result;
    }

    private CosmosConfig ParseConfig(string configJson)
    {
        // Resolve Key Vault secrets
        var resolvedElement = _secretResolver.ResolveSecretsAsync(configJson).GetAwaiter().GetResult();

        var config = JsonSerializer.Deserialize<CosmosConfig>(resolvedElement.GetRawText(), JsonSerializerOptionsProvider.Default)
            ?? throw new InvalidOperationException("Invalid Cosmos DB configuration");

        if (string.IsNullOrEmpty(config.Endpoint) || string.IsNullOrEmpty(config.Key))
        {
            throw new InvalidOperationException("Cosmos DB requires Endpoint and Key");
        }

        if (string.IsNullOrEmpty(config.Database) || string.IsNullOrEmpty(config.Container))
        {
            throw new InvalidOperationException("Cosmos DB requires Database and Container");
        }

        return config;
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }

    private class CosmosConfig
    {
        public string? Endpoint { get; set; }
        public string? Key { get; set; }
        public string? Database { get; set; }
        public string? Container { get; set; }
    }
}
