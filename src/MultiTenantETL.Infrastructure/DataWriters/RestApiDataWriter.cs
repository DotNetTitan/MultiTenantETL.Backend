using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using MultiTenantETL.Application.Connectors.DataReaders;
using MultiTenantETL.Application.Connectors.DataWriters;
using MultiTenantETL.Domain.Constants;
using MultiTenantETL.Domain.Entities;

namespace MultiTenantETL.Infrastructure.DataWriters;

public class RestApiDataWriter : IDataWriter
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<RestApiDataWriter> _logger;

    public RestApiDataWriter(IHttpClientFactory httpClientFactory, ILogger<RestApiDataWriter> logger)
    {
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<DataWriteResult> WriteBatchAsync(
        Connector connector,
        ReadBatch batch,
        WriteOptions options,
        CancellationToken cancellationToken)
    {
        var result = new DataWriteResult { BatchId = batch.BatchId };

        try
        {
            var config = ParseConfig(connector.ConfigJson);
            var httpClient = _httpClientFactory.CreateClient();

            ConfigureHttpClient(httpClient, config);

            var successCount = 0;
            var failCount = 0;

            if (config.BatchEndpoint)
            {
                var jsonContent = JsonSerializer.Serialize(batch.Rows);
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                var response = await httpClient.PostAsync(config.FullUrl, content, cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    successCount = batch.RowCount;
                }
                else
                {
                    failCount = batch.RowCount;
                    result.Errors.Add($"Batch POST failed: {response.StatusCode}");
                }
            }
            else
            {
                foreach (var row in batch.Rows)
                {
                    var jsonContent = JsonSerializer.Serialize(row);
                    var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                    var response = await httpClient.PostAsync(config.FullUrl, content, cancellationToken);

                    if (response.IsSuccessStatusCode)
                    {
                        successCount++;
                    }
                    else
                    {
                        failCount++;
                        result.Errors.Add($"Row POST failed: {response.StatusCode}");
                    }
                }
            }

            result.RowsWritten = successCount;
            result.RowsFailed = failCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write batch to REST API");
            result.RowsFailed = batch.RowCount;
            result.Errors.Add(ex.Message);
        }

        return result;
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

        // Validate base URL
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            throw new InvalidOperationException("REST API connector URL must be provided (baseUrl or Url field).");
        }

        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri))
        {
            throw new InvalidOperationException($"REST API connector URL must be an absolute URI (e.g., https://api.example.com). Provided: '{baseUrl}'");
        }

        // Determine endpoint path - support both old single EndpointPath and new endpoints array
        string? endpointPath = null;
        
        if (config.Endpoints?.Count > 0)
        {
            // Use first POST/PUT/PATCH endpoint for destination connectors
            var endpoint = config.Endpoints.FirstOrDefault(e => 
                new[] { "POST", "PUT", "PATCH" }.Contains(e.Method?.ToUpperInvariant()))
                          ?? config.Endpoints[0];
            endpointPath = endpoint.Path;
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
            
            // Validate the combined URL
            if (!Uri.TryCreate(config.FullUrl, UriKind.Absolute, out _))
            {
                throw new InvalidOperationException($"Combined URL is invalid: {config.FullUrl}");
            }
        }
        else
        {
            config.FullUrl = baseUrl;
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

    public ValueTask DisposeAsync()
    {
        // No resources to dispose for REST API writer
        return ValueTask.CompletedTask;
    }

    private class RestApiConfig
    {
        public string Url { get; set; } = string.Empty;
        public string? BaseUrl { get; set; }
        public string? EndpointPath { get; set; }
        public List<ApiEndpoint>? Endpoints { get; set; }
        public string FullUrl { get; set; } = string.Empty;
        public bool BatchEndpoint { get; set; } = false;
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
    }
}
