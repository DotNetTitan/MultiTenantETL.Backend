using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using MultiTenantETL.Application.Connectors.DataReaders;
using MultiTenantETL.Application.Connectors.DataWriters;
using MultiTenantETL.Domain.Entities;

namespace MultiTenantETL.Infrastructure.DataWriters;

public class RestApiDataWriter : IDataWriter
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<RestApiDataWriter> _logger;

    public RestApiDataWriter(IHttpClientFactory httpClientFactory, ILogger<RestApiDataWriter> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
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

                var response = await httpClient.PostAsync(config.Url, content, cancellationToken);

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

                    var response = await httpClient.PostAsync(config.Url, content, cancellationToken);

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
        return JsonSerializer.Deserialize<RestApiConfig>(configJson)
            ?? throw new InvalidOperationException("Invalid REST API configuration");
    }

    public ValueTask DisposeAsync()
    {
        // No resources to dispose for REST API writer
        return ValueTask.CompletedTask;
    }

    private class RestApiConfig
    {
        public string Url { get; set; } = string.Empty;
        public bool BatchEndpoint { get; set; } = false;
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
