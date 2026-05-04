using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MultiTenantETL.Application.Common.Interfaces;
using MultiTenantETL.Application.Connectors.DataReaders;
using MultiTenantETL.Domain.Entities;
using MultiTenantETL.Infrastructure.Configuration;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace MultiTenantETL.Infrastructure.DataReaders;

public class CosmosDbDataReader : IDataReader
{
    private readonly ILogger<CosmosDbDataReader> _logger;
    private readonly IOptions<EtlSettings> _settings;
    private readonly ISecretResolver _secretResolver;

    public CosmosDbDataReader(
        ILogger<CosmosDbDataReader> logger,
        IOptions<EtlSettings> _settings,
        ISecretResolver secretResolver)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        this._settings = _settings ?? throw new ArgumentNullException(nameof(_settings));
        _secretResolver = secretResolver ?? throw new ArgumentNullException(nameof(secretResolver));
    }

    public async IAsyncEnumerable<ReadBatch> ReadAsync(
        Connector connector,
        ReadOptions options,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var config = ParseConfig(connector.ConfigJson);
        using var client = new CosmosClient(config.Endpoint, config.Key, new CosmosClientOptions
        {
            SerializerOptions = new CosmosSerializationOptions { PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase }
        });

        var container = client.GetContainer(config.Database, config.Container);
        var query = string.IsNullOrEmpty(config.Query)
            ? "SELECT * FROM c"
            : config.Query;

        var queryDefinition = new QueryDefinition(query);
        using var feedIterator = container.GetItemQueryIterator<Dictionary<string, object?>>(queryDefinition);

        while (feedIterator.HasMoreResults)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var response = await feedIterator.ReadNextAsync(cancellationToken);

            var batch = new ReadBatch
            {
                BatchId = Guid.NewGuid(),
                Rows = new List<Dictionary<string, object?>>()
            };

            foreach (var item in response)
            {
                batch.Rows.Add(item);
            }

            if (batch.Rows.Count > 0)
            {
                yield return batch;
            }
        }
    }

    public async Task<bool> TestConnectionAsync(Connector connector, CancellationToken cancellationToken)
    {
        try
        {
            var config = ParseConfig(connector.ConfigJson);
            using var client = new CosmosClient(config.Endpoint, config.Key);
            await client.ReadAccountAsync();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Cosmos DB connection test failed");
            return false;
        }
    }

    public async Task<SchemaDetectionResult> DetectSchemaAsync(Connector connector, CancellationToken cancellationToken)
    {
        try
        {
            var config = ParseConfig(connector.ConfigJson);
            using var client = new CosmosClient(config.Endpoint, config.Key);
            var container = client.GetContainer(config.Database, config.Container);

            // Sample 10 documents
            var query = "SELECT TOP 10 * FROM c";
            using var feedIterator = container.GetItemQueryIterator<Dictionary<string, object?>>(query);

            var fieldDict = new Dictionary<string, string>();
            if (feedIterator.HasMoreResults)
            {
                var response = await feedIterator.ReadNextAsync(cancellationToken);
                foreach (var row in response)
                {
                    foreach (var kvp in row)
                    {
                        if (!fieldDict.ContainsKey(kvp.Key))
                        {
                            fieldDict[kvp.Key] = InferDataType(kvp.Value);
                        }
                    }
                }
            }

            return new SchemaDetectionResult
            {
                Success = true,
                Fields = fieldDict.Select(f => new FieldDefinition
                {
                    Name = f.Key,
                    DataType = f.Value
                }).ToList()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to detect Cosmos DB schema");
            return new SchemaDetectionResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
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

    private Dictionary<string, object?> ConvertJsonElementToDictionary(dynamic item)
    {
        string json = JsonSerializer.Serialize(item);
        var element = JsonSerializer.Deserialize<JsonElement>(json);

        var result = new Dictionary<string, object?>();
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                result[property.Name] = ConvertJsonValue(property.Value);
            }
        }
        return result;
    }

    private object? ConvertJsonValue(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.TryGetInt64(out long l) ? l : element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            _ => element.GetRawText()
        };
    }

    private string InferDataType(object? value)
    {
        if (value == null) return "varchar";
        if (value is bool) return "boolean";
        if (value is long or int) return "bigint";
        if (value is double or float or decimal) return "decimal";
        if (DateTime.TryParse(value.ToString(), out _)) return "datetime";
        return "varchar";
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
        public string? Query { get; set; }
    }
}
