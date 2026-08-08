using Microsoft.Extensions.Logging;
using MultiTenantETL.Application.Connectors.DataReaders;
using MultiTenantETL.Domain.Constants;
using MultiTenantETL.Domain.Entities;
using MultiTenantETL.Infrastructure.Security;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text.Json;
using IDataReader = MultiTenantETL.Application.Connectors.DataReaders.IDataReader;

namespace MultiTenantETL.Infrastructure.DataReaders;

public class RestApiDataReader : IDataReader
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ISsrfGuard _ssrfGuard;
    private readonly ILogger<RestApiDataReader> _logger;

    public RestApiDataReader(
        IHttpClientFactory httpClientFactory,
        ILogger<RestApiDataReader> logger,
        ISsrfGuard ssrfGuard)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _ssrfGuard = ssrfGuard;
    }

    public async IAsyncEnumerable<ReadBatch> ReadAsync(
        Connector connector,
        ReadOptions options,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var config = ParseConfig(connector.ConfigJson);
        var httpClient = _httpClientFactory.CreateClient();

        ConfigureHttpClient(httpClient, config);

        _ssrfGuard.ValidateUrl(config.FullUrl);

        var response = await httpClient.GetAsync(config.FullUrl, cancellationToken);
        response.EnsureSuccessStatusCode();

        EnsureJsonResponseFormat(config);
        EnsureJsonContentType(response);

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

            _ssrfGuard.ValidateUrl(config.FullUrl);

            var response = await httpClient.GetAsync(config.FullUrl, cancellationToken);
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

            _ssrfGuard.ValidateUrl(config.FullUrl);

            var response = await httpClient.GetAsync(config.FullUrl, cancellationToken);
            response.EnsureSuccessStatusCode();

            EnsureJsonResponseFormat(config);
            EnsureJsonContentType(response);

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

    private static void EnsureJsonResponseFormat(RestApiConfig config)
    {
        var responseFormat = config.ResponseFormat?.ToUpperInvariant() ?? ApiResponseFormats.Json;
        if (responseFormat != ApiResponseFormats.Json)
        {
            throw new NotSupportedException($"Response format '{config.ResponseFormat}' is not supported. Supported formats: {ApiResponseFormats.Json}");
        }
    }

    private static void EnsureJsonContentType(HttpResponseMessage response)
    {
        var mediaType = response.Content.Headers.ContentType?.MediaType;

        if (string.IsNullOrWhiteSpace(mediaType))
        {
            throw new InvalidOperationException("API response did not include a Content-Type header. Expected a JSON media type (application/json or */*+json).");
        }

        if (!IsJsonMediaType(mediaType))
        {
            throw new InvalidOperationException($"API response Content-Type '{mediaType}' is not JSON. Expected application/json or a +json media type.");
        }
    }

    private static bool IsJsonMediaType(string mediaType)
    {
        return mediaType.Equals("application/json", StringComparison.OrdinalIgnoreCase)
            || mediaType.Equals("text/json", StringComparison.OrdinalIgnoreCase)
            || mediaType.EndsWith("+json", StringComparison.OrdinalIgnoreCase);
    }

    private void ConfigureHttpClient(HttpClient httpClient, RestApiConfig config)
    {
        httpClient.Timeout = TimeSpan.FromSeconds(config.TimeoutSeconds);

        if (!string.IsNullOrEmpty(config.AuthType))
        {
            var authType = config.AuthType.Replace(" ", "").ToLower();

            switch (authType)
            {
                case "bearer":
                    httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", config.Token);
                    break;
                case "apikey":
                    if (!string.IsNullOrEmpty(config.ApiKey))
                    {
                        httpClient.DefaultRequestHeaders.Add(config.ApiKeyHeader ?? "X-API-Key", config.ApiKey);
                    }
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
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        var config = JsonSerializer.Deserialize<RestApiConfig>(configJson, options)
            ?? throw new InvalidOperationException("Invalid REST API configuration");

        // Support both old and new frontend formats
        var baseUrl = !string.IsNullOrWhiteSpace(config.BaseUrl) ? config.BaseUrl : config.Url;

        _logger.LogInformation("Parsing REST API config - BaseUrl: {BaseUrl}, Url: {Url}, EndpointPath: {EndpointPath}, Endpoints Count: {EndpointsCount}",
            config.BaseUrl, config.Url, config.EndpointPath, config.Endpoints?.Count ?? 0);

        // Validate base URL
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            _logger.LogError("Invalid REST API URL: empty or null. ConfigJson: {ConfigJson}", configJson);
            throw new InvalidOperationException("REST API connector URL must be provided (baseUrl or Url field).");
        }

        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri))
        {
            _logger.LogError("Invalid REST API URL: {Url}. Must be an absolute URI.", baseUrl);
            throw new InvalidOperationException($"REST API connector URL must be an absolute URI (e.g., https://api.example.com). Provided: '{baseUrl}'");
        }

        // Determine endpoint path - support both old single EndpointPath and new endpoints array
        string? endpointPath = null;

        if (config.Endpoints?.Count > 0)
        {
            // Use first GET endpoint for source connectors
            var endpoint = config.Endpoints.FirstOrDefault(e => e.Method?.Equals("GET", StringComparison.OrdinalIgnoreCase) == true)
                          ?? config.Endpoints[0];
            endpointPath = endpoint.Path;

            // Use responseDataPath if available
            if (!string.IsNullOrWhiteSpace(endpoint.ResponseDataPath))
            {
                config.DataPath = endpoint.ResponseDataPath;
            }
        }
        else if (!string.IsNullOrWhiteSpace(config.EndpointPath))
        {
            endpointPath = config.EndpointPath;
        }

        // Combine base URL with endpoint path if provided
        if (!string.IsNullOrWhiteSpace(endpointPath))
        {
            endpointPath = endpointPath.TrimStart('/');
            var baseUrlTrimmed = baseUrl.TrimEnd('/');
            config.FullUrl = $"{baseUrlTrimmed}/{endpointPath}";

            _logger.LogInformation("Combined URL: {FullUrl}", config.FullUrl);

            // Validate the combined URL
            if (!Uri.TryCreate(config.FullUrl, UriKind.Absolute, out _))
            {
                throw new InvalidOperationException($"Combined URL is invalid: {config.FullUrl}");
            }
        }
        else
        {
            config.FullUrl = baseUrl;
            _logger.LogInformation("Using base URL as full URL: {FullUrl}", config.FullUrl);
        }

        // Support both old and new auth token field names
        if (string.IsNullOrWhiteSpace(config.Token) && !string.IsNullOrWhiteSpace(config.AuthToken))
        {
            config.Token = config.AuthToken;
        }

        // Support both old and new API key field names
        if (string.IsNullOrWhiteSpace(config.ApiKey) && !string.IsNullOrWhiteSpace(config.ApiKeyValue))
        {
            config.ApiKey = config.ApiKeyValue;
        }

        return config;
    }

    private class RestApiConfig
    {
        public string Url { get; set; } = string.Empty;
        public string? BaseUrl { get; set; }
        public string? EndpointPath { get; set; }
        public List<ApiEndpoint>? Endpoints { get; set; }
        public string FullUrl { get; set; } = string.Empty;
        public string? DataPath { get; set; }
        public string? ResponseFormat { get; set; } = ApiResponseFormats.Json;
        public string? AuthType { get; set; }
        public string? Token { get; set; }
        public string? AuthToken { get; set; }
        public string? ApiKey { get; set; }
        public string? ApiKeyValue { get; set; }
        public string? ApiKeyHeader { get; set; }
        public string? Username { get; set; }
        public string? Password { get; set; }
        public Dictionary<string, string>? Headers { get; set; }
        public int TimeoutSeconds { get; set; } = 30;
    }

    private class ApiEndpoint
    {
        public string? Path { get; set; }
        public string? Method { get; set; }
        public string? ResponseDataPath { get; set; }
    }
}
