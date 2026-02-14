using System.Linq;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;
using MultiTenantETL.Application.Common.Interfaces;
using MultiTenantETL.Application.Connectors.DataReaders;
using MultiTenantETL.Application.Connectors.DataWriters;
using MultiTenantETL.Domain.Entities;
using MultiTenantETL.Infrastructure.Configuration;
using MultiTenantETL.Infrastructure.Security;

namespace MultiTenantETL.Infrastructure.DataWriters;

/// <summary>
/// Writes data to MongoDB collections.
/// Supports bulk inserts and upserts.
/// </summary>
public class MongoDbDataWriter : IDataWriter
{
    private readonly ILogger<MongoDbDataWriter> _logger;
    private readonly ISecretResolver _secretResolver;

    public MongoDbDataWriter(ILogger<MongoDbDataWriter> logger, ISecretResolver secretResolver)
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
        cancellationToken.ThrowIfCancellationRequested();
        
        var result = new DataWriteResult { BatchId = batch.BatchId };
        var config = ParseConfig(connector.ConfigJson);
        
        try
        {
            var client = new MongoClient(config.ConnectionString);
            var database = client.GetDatabase(config.Database);
            var collection = database.GetCollection<BsonDocument>(config.CollectionName);

            if (options.TruncateBeforeLoad)
            {
                _logger.LogInformation("Dropping MongoDB collection {CollectionName}", config.CollectionName);
                await database.DropCollectionAsync(config.CollectionName, cancellationToken);
            }

            if (batch.Rows.Count == 0)
            {
                return result;
            }

            var documents = batch.Rows.Select(row => ConvertToBsonDocument(row)).ToList();

            if (options.UseUpsert && options.UpsertKeys?.Count > 0)
            {
                var bulkOps = new List<WriteModel<BsonDocument>>();
                foreach (var doc in documents)
                {
                    var filter = BuildUpsertFilter(doc, options.UpsertKeys);
                    bulkOps.Add(new ReplaceOneModel<BsonDocument>(filter, doc) { IsUpsert = true });
                }
                var bulkResult = await collection.BulkWriteAsync(bulkOps, null, cancellationToken);
                result.RowsWritten = (int)(bulkResult.InsertedCount + bulkResult.ModifiedCount + bulkResult.Upserts.Count());
            }
            else
            {
                await collection.InsertManyAsync(documents, null, cancellationToken);
                result.RowsWritten = batch.RowCount;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write batch to MongoDB");
            result.RowsFailed = batch.RowCount;
            result.Errors.Add(ex.Message);
        }

        return result;
    }

    private BsonDocument ConvertToBsonDocument(Dictionary<string, object?> row)
    {
        var document = new BsonDocument();
        foreach (var kvp in row)
        {
            document[kvp.Key] = BsonValue.Create(kvp.Value);
        }
        return document;
    }

    private FilterDefinition<BsonDocument> BuildUpsertFilter(BsonDocument doc, List<string> keys)
    {
        var filterBuilder = Builders<BsonDocument>.Filter;
        var filters = keys.Select(key => filterBuilder.Eq(key, doc[key])).ToList();
        return filterBuilder.And(filters);
    }

    private MongoDbConfig ParseConfig(string configJson)
    {
        // Resolve Key Vault secrets
        var resolvedElement = _secretResolver.ResolveSecretsAsync(configJson).GetAwaiter().GetResult();
        
        var config = JsonSerializer.Deserialize<MongoDbConfig>(resolvedElement.GetRawText(), JsonSerializerOptionsProvider.Default)
            ?? throw new InvalidOperationException("Invalid MongoDB configuration");

        if (string.IsNullOrEmpty(config.CollectionName) && config.WriteConfig != null)
        {
            config.CollectionName = config.WriteConfig.CollectionName ?? string.Empty;
        }

        if (string.IsNullOrEmpty(config.CollectionName))
        {
            throw new InvalidOperationException("MongoDB configuration must include CollectionName");
        }

        return config;
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }

    private class MongoDbConfig
    {
        public string ConnectionString { get; set; } = null!;
        public string Database { get; set; } = null!;
        public string CollectionName { get; set; } = null!;
        public WriteConfigSection? WriteConfig { get; set; }
    }

    private class WriteConfigSection
    {
        public string? CollectionName { get; set; }
    }
}
