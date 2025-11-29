using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace MultiTenantETL.Infrastructure.Services.Http;

public interface IHttpClientAuthenticator
{
    Task AuthenticateAsync(HttpClient httpClient, ApiAuthConfig authConfig, CancellationToken cancellationToken = default);
}

public class HttpClientAuthenticator : IHttpClientAuthenticator
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<HttpClientAuthenticator> _logger;

    public HttpClientAuthenticator(IHttpClientFactory httpClientFactory, ILogger<HttpClientAuthenticator> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task AuthenticateAsync(HttpClient httpClient, ApiAuthConfig authConfig, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(authConfig.AuthType))
            return;

        var authType = authConfig.AuthType.ToLower().Replace(" ", "");

        switch (authType)
        {
            case "bearer":
                await AddBearerAuthAsync(httpClient, authConfig, cancellationToken);
                break;

            case "basic":
                AddBasicAuth(httpClient, authConfig);
                break;

            case "apikey":
                AddApiKeyAuth(httpClient, authConfig);
                break;

            case "oauth2":
            case "oauth2clientcredentials":
                await AddOAuth2AuthAsync(httpClient, authConfig, cancellationToken);
                break;

            default:
                _logger.LogWarning("Unsupported authentication type: {AuthType}", authConfig.AuthType);
                break;
        }

        // Add custom headers
        if (authConfig.Headers != null)
        {
            foreach (var header in authConfig.Headers)
            {
                httpClient.DefaultRequestHeaders.TryAddWithoutValidation(header.Key, header.Value);
            }
        }
    }

    private async Task AddBearerAuthAsync(HttpClient httpClient, ApiAuthConfig authConfig, CancellationToken cancellationToken)
    {
        string? token = null;

        if (authConfig.UseDynamicToken && !string.IsNullOrEmpty(authConfig.TokenEndpointUrl))
        {
            token = await GenerateDynamicTokenAsync(authConfig, cancellationToken);
        }
        else
        {
            token = authConfig.Token;
        }

        if (!string.IsNullOrEmpty(token))
        {
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
    }

    private void AddBasicAuth(HttpClient httpClient, ApiAuthConfig authConfig)
    {
        if (!string.IsNullOrEmpty(authConfig.Username) && !string.IsNullOrEmpty(authConfig.Password))
        {
            var credentials = Convert.ToBase64String(
                System.Text.Encoding.ASCII.GetBytes($"{authConfig.Username}:{authConfig.Password}"));
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);
        }
    }

    private void AddApiKeyAuth(HttpClient httpClient, ApiAuthConfig authConfig)
    {
        if (!string.IsNullOrEmpty(authConfig.ApiKeyHeader) && !string.IsNullOrEmpty(authConfig.ApiKey))
        {
            httpClient.DefaultRequestHeaders.Add(authConfig.ApiKeyHeader, authConfig.ApiKey);
        }
    }

    private async Task AddOAuth2AuthAsync(HttpClient httpClient, ApiAuthConfig authConfig, CancellationToken cancellationToken)
    {
        var token = await GenerateDynamicTokenAsync(authConfig, cancellationToken);
        if (!string.IsNullOrEmpty(token))
        {
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
    }

    private async Task<string?> GenerateDynamicTokenAsync(ApiAuthConfig authConfig, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(authConfig.TokenEndpointUrl))
        {
            _logger.LogWarning("Token endpoint URL is required for dynamic token generation");
            return null;
        }

        try
        {
            var tokenClient = _httpClientFactory.CreateClient();
            tokenClient.Timeout = TimeSpan.FromSeconds(authConfig.TokenTimeoutSeconds);

            var request = new HttpRequestMessage
            {
                Method = authConfig.TokenEndpointMethod?.ToUpper() == "GET" ? HttpMethod.Get : HttpMethod.Post,
                RequestUri = new Uri(authConfig.TokenEndpointUrl)
            };

            // Add token endpoint headers
            if (authConfig.TokenEndpointHeaders != null)
            {
                foreach (var header in authConfig.TokenEndpointHeaders)
                {
                    request.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }
            }

            // Add body for POST requests
            if (request.Method == HttpMethod.Post && !string.IsNullOrEmpty(authConfig.TokenEndpointBody))
            {
                request.Content = new StringContent(
                    authConfig.TokenEndpointBody,
                    System.Text.Encoding.UTF8,
                    "application/json");
            }

            var response = await tokenClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("Token endpoint returned {StatusCode}: {Error}", response.StatusCode, errorContent);
                return null;
            }

            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var tokenJson = JsonSerializer.Deserialize<JsonElement>(responseContent);

            var tokenPath = authConfig.TokenResponsePath ?? "access_token";
            var token = ExtractTokenFromResponse(tokenJson, tokenPath);

            if (string.IsNullOrEmpty(token))
            {
                _logger.LogWarning("Could not extract token from response using path '{TokenPath}'", tokenPath);
            }

            return token;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate dynamic token from {TokenEndpoint}", authConfig.TokenEndpointUrl);
            return null;
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

public class ApiAuthConfig
{
    public string? AuthType { get; set; }
    public string? Token { get; set; }
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string? ApiKey { get; set; }
    public string? ApiKeyHeader { get; set; }
    public Dictionary<string, string>? Headers { get; set; }

    // Dynamic token generation
    public bool UseDynamicToken { get; set; }
    public string? TokenEndpointUrl { get; set; }
    public string? TokenEndpointMethod { get; set; } = "POST";
    public string? TokenEndpointBody { get; set; }
    public Dictionary<string, string>? TokenEndpointHeaders { get; set; }
    public string? TokenResponsePath { get; set; } = "access_token";
    public int TokenTimeoutSeconds { get; set; } = 30;
}
