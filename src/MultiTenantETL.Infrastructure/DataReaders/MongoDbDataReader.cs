using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using MultiTenantETL.Application.Common.Interfaces;
using MultiTenantETL.Application.Connectors.DataReaders;
using MultiTenantETL.Domain.Entities;
using MultiTenantETL.Infrastructure.Configuration;
using System.Runtime.CompilerServices;
using System.Text.Json;
using IDataReader = MultiTenantETL.Application.Connectors.DataReaders.IDataReader;

namespace MultiTenantETL.Infrastructure.DataReaders;

/// <summary>
/// Reads data from MongoDB collections.
/// Supports basic filtering and batching.
/// </summary>
public class MongoDbDataReader : IDataReader
{
    private readonly ILogger<MongoDbDataReader> _logger;
    private readonly EtlSettings _settings;
    private readonly ISecretResolver _secretResolver;

    public MongoDbDataReader(
        ILogger<MongoDbDataReader> logger,
        IOptions<EtlSettings> settings,
        ISecretResolver secretResolver)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _settings = settings?.Value ?? throw new ArgumentNullException(nameof(settings));
        _secretResolver = secretResolver ?? throw new ArgumentNullException(nameof(secretResolver));
    }

    public async IAsyncEnumerable<ReadBatch> ReadAsync(
        Connector connector,
        ReadOptions options,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var config = ParseConfig(connector.ConfigJson);
        var client = new MongoClient(config.ConnectionString);
        var database = client.GetDatabase(config.Database);
        var collection = database.GetCollection<BsonDocument>(config.CollectionName);

        var filter = string.IsNullOrEmpty(config.FilterJson)
            ? FilterDefinition<BsonDocument>.Empty
            : BsonDocument.Parse(config.FilterJson);

        var findOptions = new FindOptions<BsonDocument>();
        if (options.MaxRows.HasValue)
        {
            findOptions.Limit = options.MaxRows.Value;
        }

        using var cursor = await collection.FindAsync(filter, findOptions, cancellationToken);

        var batch = new ReadBatch { BatchId = Guid.NewGuid() };
        var rowsRead = 0;

        while (await cursor.MoveNextAsync(cancellationToken))
        {
            foreach (var document in cursor.Current)
            {
                var row = ConvertToDictionary(document);
                batch.Rows.Add(row);
                batch.RowCount++;
                rowsRead++;

                if (batch.RowCount >= options.BatchSize)
                {
                    yield return batch;
                    batch = new ReadBatch { BatchId = Guid.NewGuid() };
                }
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
            var client = new MongoClient(config.ConnectionString);
            var database = client.GetDatabase(config.Database);
            await database.RunCommandAsync((Command<BsonDocument>)"{ping:1}", cancellationToken: cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MongoDB connection test failed");
            return false;
        }
    }

    public async Task<SchemaDetectionResult> DetectSchemaAsync(Connector connector, CancellationToken cancellationToken)
    {
        try
        {
            var config = ParseConfig(connector.ConfigJson);
            var client = new MongoClient(config.ConnectionString);
            var database = client.GetDatabase(config.Database);
            var collection = database.GetCollection<BsonDocument>(config.CollectionName);

            // Fetch a few documents to samples types
            var sampleDocs = await collection.Find(FilterDefinition<BsonDocument>.Empty)
                .Limit(10)
                .ToListAsync(cancellationToken);

            if (sampleDocs.Count == 0)
            {
                return new SchemaDetectionResult
                {
                    Success = false,
                    ErrorMessage = "Collection is empty, cannot detect schema",
                    DetectedAt = DateTimeOffset.UtcNow
                };
            }

            var fieldMap = new Dictionary<string, FieldDefinition>();

            foreach (var doc in sampleDocs)
            {
                foreach (var element in doc.Elements)
                {
                    if (fieldMap.ContainsKey(element.Name)) continue;

                    fieldMap[element.Name] = new FieldDefinition
                    {
                        Name = element.Name,
                        DataType = MapBsonTypeToDataType(element.Value.BsonType),
                        IsNullable = true,
                        IsPrimaryKey = element.Name == "_id"
                    };
                }
            }

            return new SchemaDetectionResult
            {
                Fields = fieldMap.Values.ToList(),
                Version = 1,
                DetectedAt = DateTimeOffset.UtcNow,
                Success = true
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Schema detection failed for MongoDB");
            return new SchemaDetectionResult
            {
                Success = false,
                ErrorMessage = ex.Message,
                DetectedAt = DateTimeOffset.UtcNow
            };
        }
    }

    private Dictionary<string, object?> ConvertToDictionary(BsonDocument document)
    {
        var dictionary = new Dictionary<string, object?>();
        foreach (var element in document.Elements)
        {
            dictionary[element.Name] = ConvertBsonValue(element.Value);
        }
        return dictionary;
    }

    private object? ConvertBsonValue(BsonValue value)
    {
        if (value.IsBsonNull) return null;
        if (value.IsGuid) return value.AsGuid;
        if (value.IsBoolean) return value.AsBoolean;
        if (value.BsonType == BsonType.DateTime) return value.ToUniversalTime();
        if (value.IsDouble) return value.AsDouble;
        if (value.IsInt32) return value.AsInt32;
        if (value.IsInt64) return value.AsInt64;
        if (value.IsString) return value.AsString;
        if (value.IsObjectId) return value.AsObjectId.ToString();
        if (value.IsDecimal128) return (decimal)value.AsDecimal128;

        // Complex types returned as string representation or serialized JSON
        return value.ToString();
    }

    private string MapBsonTypeToDataType(BsonType type)
    {
        return type switch
        {
            BsonType.Boolean => "boolean",
            BsonType.DateTime => "datetime",
            BsonType.Double => "double",
            BsonType.Int32 => "int",
            BsonType.Int64 => "long",
            BsonType.String => "string",
            BsonType.ObjectId => "string",
            BsonType.Decimal128 => "decimal",
            BsonType.Binary => "binary",
            _ => "string"
        };
    }

    private MongoDbConfig ParseConfig(string configJson)
    {
        // Resolve Key Vault secrets
        var resolvedElement = _secretResolver.ResolveSecretsAsync(configJson).GetAwaiter().GetResult();

        var config = JsonSerializer.Deserialize<MongoDbConfig>(resolvedElement.GetRawText(), JsonSerializerOptionsProvider.Default)
            ?? throw new InvalidOperationException("Invalid MongoDB configuration");

        return config;
    }

    private class MongoDbConfig
    {
        public string ConnectionString { get; set; } = null!;
        public string Database { get; set; } = null!;
        public string CollectionName { get; set; } = null!;
        public string? FilterJson { get; set; }
    }
}
