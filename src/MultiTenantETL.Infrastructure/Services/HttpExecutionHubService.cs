using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MultiTenantETL.Application.Executions;
using MultiTenantETL.Application.Executions.Models;

namespace MultiTenantETL.Infrastructure.Services;

/// <summary>
/// HTTP-based ExecutionHubService for Worker context
/// Sends SignalR updates by calling back to the API's internal broadcast endpoints
/// </summary>
public class HttpExecutionHubService : IExecutionHubService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<HttpExecutionHubService> _logger;
    private readonly string _apiBaseUrl;
    private readonly bool _isEnabled;

    public HttpExecutionHubService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<HttpExecutionHubService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        
        // Get API base URL from configuration
        _apiBaseUrl = configuration["SignalR:ApiBaseUrl"] ?? "http://localhost:5000";
        _isEnabled = configuration.GetValue<bool>("SignalR:Enabled", true);

        if (!_isEnabled)
        {
            _logger.LogInformation("SignalR broadcasting is disabled");
        }
    }

    public async Task SendLogAsync(Guid executionId, Guid tenantId, ExecutionLogDto log, CancellationToken cancellationToken = default)
    {
        if (!_isEnabled)
        {
            _logger.LogTrace("SignalR disabled, skipping log broadcast");
            return;
        }

        try
        {
            var request = new
            {
                ExecutionId = executionId,
                TenantId = tenantId,
                Log = log
            };

            var response = await _httpClient.PostAsJsonAsync(
                $"{_apiBaseUrl}/api/internal/signalr/log",
                request,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Failed to broadcast log for execution {ExecutionId}: {StatusCode}",
                    executionId, response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            // Don't fail the execution if SignalR broadcast fails
            _logger.LogError(ex, "Error broadcasting log for execution {ExecutionId}", executionId);
        }
    }

    public async Task SendStatusUpdateAsync(Guid executionId, Guid tenantId, ExecutionStatusUpdate update, CancellationToken cancellationToken = default)
    {
        if (!_isEnabled)
        {
            _logger.LogTrace("SignalR disabled, skipping status broadcast");
            return;
        }

        try
        {
            var request = new
            {
                ExecutionId = executionId,
                TenantId = tenantId,
                Update = update
            };

            var response = await _httpClient.PostAsJsonAsync(
                $"{_apiBaseUrl}/api/internal/signalr/status",
                request,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Failed to broadcast status for execution {ExecutionId}: {StatusCode}",
                    executionId, response.StatusCode);
            }
            else
            {
                _logger.LogDebug("Broadcast status update for execution {ExecutionId}: {Status}", 
                    executionId, update.Status);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error broadcasting status for execution {ExecutionId}", executionId);
        }
    }

    public async Task SendStatsUpdateAsync(Guid executionId, Guid tenantId, ExecutionProgressUpdate stats, CancellationToken cancellationToken = default)
    {
        if (!_isEnabled)
        {
            _logger.LogTrace("SignalR disabled, skipping progress broadcast");
            return;
        }

        try
        {
            var request = new
            {
                ExecutionId = executionId,
                TenantId = tenantId,
                Progress = stats
            };

            var response = await _httpClient.PostAsJsonAsync(
                $"{_apiBaseUrl}/api/internal/signalr/progress",
                request,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Failed to broadcast progress for execution {ExecutionId}: {StatusCode}",
                    executionId, response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error broadcasting progress for execution {ExecutionId}", executionId);
        }
    }

    public async Task SendCompletionAsync(Guid executionId, Guid tenantId, ExecutionCompletionUpdate completion, CancellationToken cancellationToken = default)
    {
        if (!_isEnabled)
        {
            _logger.LogTrace("SignalR disabled, skipping completion broadcast");
            return;
        }

        try
        {
            var request = new
            {
                ExecutionId = executionId,
                TenantId = tenantId,
                Completion = completion
            };

            var response = await _httpClient.PostAsJsonAsync(
                $"{_apiBaseUrl}/api/internal/signalr/completion",
                request,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Failed to broadcast completion for execution {ExecutionId}: {StatusCode}",
                    executionId, response.StatusCode);
            }
            else
            {
                _logger.LogInformation("Broadcast completion for execution {ExecutionId}: {Status}", 
                    executionId, completion.Status);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error broadcasting completion for execution {ExecutionId}", executionId);
        }
    }
}
