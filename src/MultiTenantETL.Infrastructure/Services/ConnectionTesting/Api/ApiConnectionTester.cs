using System.Text.Json;
using Microsoft.Extensions.Logging;
using MultiTenantETL.Application.Connectors;
using MultiTenantETL.Application.Connectors.Models;

namespace MultiTenantETL.Infrastructure.Services.ConnectionTesting.Api;

public class ApiConnectionTester : IApiConnectionTester
{
    private readonly ILogger<ApiConnectionTester> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public ApiConnectionTester(ILogger<ApiConnectionTester> logger, IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<ConnectionTestResult> TestConnectionAsync(string provider, JsonElement config)
    {
        var apiConfig = JsonSerializer.Deserialize<ApiConfig>(config, JsonOptions);
        if (apiConfig == null)
        {
            return new ConnectionTestResult
            {
                Success = false,
                Message = "Invalid API configuration"
            };
        }

        // Validate required fields
        if (string.IsNullOrEmpty(apiConfig.BaseUrl))
        {
            return new ConnectionTestResult
            {
                Success = false,
                Message = "Base URL is required"
            };
        }

        try
        {
            var httpClient = _httpClientFactory.CreateClient();
            httpClient.Timeout = TimeSpan.FromSeconds(apiConfig.TimeoutSeconds);
            httpClient.BaseAddress = new Uri(apiConfig.BaseUrl);

            // Add authentication
            if (!string.IsNullOrEmpty(apiConfig.AuthType))
            {
                await AddAuthenticationAsync(httpClient, apiConfig);
            }

            // Add custom headers
            if (apiConfig.Headers != null)
            {
                foreach (var header in apiConfig.Headers)
                {
                    httpClient.DefaultRequestHeaders.Add(header.Key, header.Value);
                }
            }

            // Test connection using first GET endpoint if available, otherwise test root
            string testPath = "/";
            if (apiConfig.Endpoints != null && apiConfig.Endpoints.Count > 0)
            {
                var getEndpoint = apiConfig.Endpoints.FirstOrDefault(e => e.Method.Equals("GET", StringComparison.OrdinalIgnoreCase));
                if (getEndpoint != null)
                {
                    testPath = getEndpoint.Path;
                }
            }
            
            var response = await httpClient.GetAsync(testPath);
            
            var details = new Dictionary<string, object>
            {
                ["StatusCode"] = (int)response.StatusCode,
                ["IsSuccessStatusCode"] = response.IsSuccessStatusCode,
                ["BaseUrl"] = apiConfig.BaseUrl,
                ["TestPath"] = testPath,
                ["UsedDynamicToken"] = apiConfig.UseDynamicToken
            };

            return new ConnectionTestResult
            {
                Success = response.IsSuccessStatusCode,
                Message = response.IsSuccessStatusCode 
                    ? $"Successfully connected to API at {testPath}" 
                    : $"API returned status code {response.StatusCode} for {testPath}",
                Details = details
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "API connection test failed");
            return new ConnectionTestResult
            {
                Success = false,
                Message = $"API connection failed: {ex.Message}"
            };
        }
    }

    private async Task AddAuthenticationAsync(HttpClient httpClient, ApiConfig apiConfig)
    {
        var authType = apiConfig.AuthType.ToLower().Replace(" ", "");
        switch (authType)
        {
            case "bearer":
                string? token = null;
                
                if (apiConfig.UseDynamicToken)
                {
                    var tokenResult = await GenerateDynamicTokenAsync(apiConfig);
                    if (!tokenResult.Success)
                    {
                        throw new InvalidOperationException($"Failed to generate dynamic token: {tokenResult.Message}");
                    }
                    token = tokenResult.Token;
                }
                else
                {
                    token = apiConfig.AuthToken;
                }
                
                if (!string.IsNullOrEmpty(token))
                {
                    httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
                }
                break;
                
            case "basic":
                if (!string.IsNullOrEmpty(apiConfig.Username) && !string.IsNullOrEmpty(apiConfig.Password))
                {
                    var credentials = Convert.ToBase64String(
                        System.Text.Encoding.ASCII.GetBytes($"{apiConfig.Username}:{apiConfig.Password}"));
                    httpClient.DefaultRequestHeaders.Add("Authorization", $"Basic {credentials}");
                }
                break;
                
            case "apikey":
                if (!string.IsNullOrEmpty(apiConfig.ApiKeyHeader) && !string.IsNullOrEmpty(apiConfig.ApiKeyValue))
                {
                    httpClient.DefaultRequestHeaders.Add(apiConfig.ApiKeyHeader, apiConfig.ApiKeyValue);
                }
                break;
        }
    }

    private async Task<(bool Success, string? Token, string Message)> GenerateDynamicTokenAsync(ApiConfig apiConfig)
    {
        if (string.IsNullOrEmpty(apiConfig.TokenEndpointUrl))
        {
            return (false, null, "Token endpoint URL is required for dynamic token generation");
        }

        try
        {
            var tokenClient = _httpClientFactory.CreateClient();
            tokenClient.Timeout = TimeSpan.FromSeconds(30);
            
            tokenClient.DefaultRequestHeaders.Add("User-Agent", "MultiTenantETL/1.0");
            tokenClient.DefaultRequestHeaders.Add("Accept", "*/*");

            var request = new HttpRequestMessage
            {
                Method = apiConfig.TokenEndpointMethod?.ToUpper() == "GET" ? HttpMethod.Get : HttpMethod.Post,
                RequestUri = new Uri(apiConfig.TokenEndpointUrl)
            };

            if (request.Method == HttpMethod.Post && !string.IsNullOrEmpty(apiConfig.TokenEndpointBody))
            {
                request.Content = new StringContent(
                    apiConfig.TokenEndpointBody,
                    System.Text.Encoding.UTF8,
                    "application/json");
            }

            if (apiConfig.TokenEndpointHeaders != null)
            {
                foreach (var header in apiConfig.TokenEndpointHeaders)
                {
                    if (header.Key.Equals("Content-Type", StringComparison.OrdinalIgnoreCase))
                        continue;
                        
                    request.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }
            }

            var response = await tokenClient.SendAsync(request);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                return (false, null, $"Token endpoint returned {response.StatusCode}: {errorContent}");
            }

            var responseContent = await response.Content.ReadAsStringAsync();
            var tokenJson = JsonSerializer.Deserialize<JsonElement>(responseContent);

            var tokenPath = apiConfig.TokenResponsePath ?? "access_token";
            var token = ExtractTokenFromResponse(tokenJson, tokenPath);

            if (string.IsNullOrEmpty(token))
            {
                return (false, null, $"Could not extract token from response using path '{tokenPath}'");
            }

            return (true, token, "Token generated successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate dynamic token from {TokenEndpoint}", apiConfig.TokenEndpointUrl);
            return (false, null, $"Token generation failed: {ex.Message}");
        }
    }

    private static string? ExtractTokenFromResponse(JsonElement json, string path)
    {
        try
        {
            if (!path.Contains('.'))
            {
                if (json.TryGetProperty(path, out var value))
                {
                    return value.GetString();
                }
                return null;
            }

            var parts = path.Split('.');
            var current = json;
            
            foreach (var part in parts)
            {
                if (current.TryGetProperty(part, out var next))
                {
                    current = next;
                }
                else
                {
                    return null;
                }
            }

            return current.GetString();
        }
        catch
        {
            // Token extraction failed, likely invalid path or JSON structure
            return null;
        }
    }
}
