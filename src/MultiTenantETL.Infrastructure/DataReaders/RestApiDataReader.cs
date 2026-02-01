using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using MultiTenantETL.Application.Connectors.DataReaders;
using MultiTenantETL.Domain.Entities;
using IDataReader = MultiTenantETL.Application.Connectors.DataReaders.IDataReader;

namespace MultiTenantETL.Infrastructure.DataReaders;

public class RestApiDataReader : IDataReader
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<RestApiDataReader> _logger;

    public RestApiDataReader(IHttpClientFactory httpClientFactory, ILogger<RestApiDataReader> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async IAsyncEnumerable<ReadBatch> ReadAsync(
        Connector connector,
        ReadOptions options,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var config = ParseConfig(connector.ConfigJson);
        var httpClient = _httpClientFactory.CreateClient();

        ConfigureHttpClient(httpClient, config);

        var response = await httpClient.GetAsync(config.Url, cancellationToken);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        var jsonDoc = JsonDocument.Parse(content);

        List<Dictionary<string, object?>> rows = new();

        if (!string.IsNullOrEmpty(config.DataPath))
        {
            var element = jsonDoc.RootElement;
            foreach (var segment in config.DataPath.Split('.'))
            {
                element = element.GetProperty(segment);
            }

            if (element.ValueKind == JsonValueKind.Array)
            {
                rows = ParseJsonArray(element);
            }
            else
            {
                rows.Add(ParseJsonObject(element));
            }
        }
        else
        {
            if (jsonDoc.RootElement.ValueKind == JsonValueKind.Array)
            {
                rows = ParseJsonArray(jsonDoc.RootElement);
            }
            else
            {
                rows.Add(ParseJsonObject(jsonDoc.RootElement));
            }
        }

        var batch = new ReadBatch { BatchId = Guid.NewGuid() };
        var rowsRead = 0;

        foreach (var row in rows)
        {
            batch.Rows.Add(row);
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
        var config = ParseConfig(connector.ConfigJson);
        
        try
        {
            var httpClient = _httpClientFactory.CreateClient();

            ConfigureHttpClient(httpClient, config);

            var response = await httpClient.GetAsync(config.Url, cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "REST API connection test failed");
            return false;
        }
    }

    public async Task<SchemaDetectionResult> DetectSchemaAsync(Connector connector, CancellationToken cancellationToken)
    {
        try
        {
            var config = ParseConfig(connector.ConfigJson);
            var httpClient = _httpClientFactory.CreateClient();

            ConfigureHttpClient(httpClient, config);

            var response = await httpClient.GetAsync(config.Url, cancellationToken);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var jsonDoc = JsonDocument.Parse(content);

            Dictionary<string, object?>? firstRow = null;

            if (!string.IsNullOrEmpty(config.DataPath))
            {
                var element = jsonDoc.RootElement;
                foreach (var segment in config.DataPath.Split('.'))
                {
                    element = element.GetProperty(segment);
                }

                if (element.ValueKind == JsonValueKind.Array && element.GetArrayLength() > 0)
                {
                    firstRow = ParseJsonObject(element[0]);
                }
            }
            else if (jsonDoc.RootElement.ValueKind == JsonValueKind.Array && jsonDoc.RootElement.GetArrayLength() > 0)
            {
                firstRow = ParseJsonObject(jsonDoc.RootElement[0]);
            }
            else if (jsonDoc.RootElement.ValueKind == JsonValueKind.Object)
            {
                firstRow = ParseJsonObject(jsonDoc.RootElement);
            }

            var fields = firstRow?.Keys.Select(k => new FieldDefinition
            {
                Name = k,
                DataType = "string",
                IsNullable = true,
                IsPrimaryKey = false
            }).ToList() ?? new List<FieldDefinition>();

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
            _logger.LogError(ex, "Schema detection failed for REST API");
            return new SchemaDetectionResult
            {
                Success = false,
                ErrorMessage = ex.Message,
                DetectedAt = DateTimeOffset.UtcNow
            };
        }
    }

    private void ConfigureHttpClient(HttpClient httpClient, RestApiConfig config)
    {
        httpClient.Timeout = TimeSpan.FromSeconds(config.TimeoutSeconds);

        if (!string.IsNullOrEmpty(config.AuthType))
        {
            switch (config.AuthType.ToLower())
            {
                case "bearer":
                    httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", config.Token);
                    break;
                case "apikey":
                    httpClient.DefaultRequestHeaders.Add(config.ApiKeyHeader ?? "X-API-Key", config.ApiKey);
                    break;
                case "basic":
                    var credentials = Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes($"{config.Username}:{config.Password}"));
                    httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);
                    break;
            }
        }

        if (config.Headers != null)
        {
            foreach (var header in config.Headers)
            {
                httpClient.DefaultRequestHeaders.Add(header.Key, header.Value);
            }
        }
    }

    private List<Dictionary<string, object?>> ParseJsonArray(JsonElement arrayElement)
    {
        var rows = new List<Dictionary<string, object?>>();
        foreach (var item in arrayElement.EnumerateArray())
        {
            rows.Add(ParseJsonObject(item));
        }
        return rows;
    }

    private Dictionary<string, object?> ParseJsonObject(JsonElement objectElement)
    {
        var row = new Dictionary<string, object?>();
        foreach (var property in objectElement.EnumerateObject())
        {
            row[property.Name] = property.Value.ValueKind switch
            {
                JsonValueKind.String => property.Value.GetString(),
                JsonValueKind.Number => property.Value.GetDecimal(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Null => null,
                _ => property.Value.ToString()
            };
        }
        return row;
    }

    private RestApiConfig ParseConfig(string configJson)
    {
        var config = JsonSerializer.Deserialize<RestApiConfig>(configJson)
            ?? throw new InvalidOperationException("Invalid REST API configuration");

        if (!Uri.TryCreate(config.Url, UriKind.Absolute, out _))
        {
            throw new InvalidOperationException("REST API connector URL must be an absolute URI (e.g., https://api.example.com/data).");
        }

        return config;
    }

    private class RestApiConfig
    {
        public string Url { get; set; } = string.Empty;
        public string? DataPath { get; set; }
        public string? AuthType { get; set; }
        public string? Token { get; set; }
        public string? ApiKey { get; set; }
        public string? ApiKeyHeader { get; set; }
        public string? Username { get; set; }
        public string? Password { get; set; }
        public Dictionary<string, string>? Headers { get; set; }
        public int TimeoutSeconds { get; set; } = 30;
    }
}
