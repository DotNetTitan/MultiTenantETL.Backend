using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using MultiTenantETL.Application.Executions.Models;
using MultiTenantETL.Application.Executions.Notifications;

namespace MultiTenantETL.Infrastructure.Services;

/// <summary>
/// HTTP-based implementation of IExecutionNotificationService for Worker scenarios.
/// Sends notifications to API which broadcasts them via SignalR.
/// </summary>
public class HttpExecutionNotificationService : IExecutionNotificationService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<HttpExecutionNotificationService> _logger;

    public HttpExecutionNotificationService(
        HttpClient httpClient,
        ILogger<HttpExecutionNotificationService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task NotifyExecutionStatusChangedAsync(
        Guid tenantId,
        ExecutionStatusUpdate update,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var notification = new
            {
                TenantId = tenantId,
                Update = update
            };

            var response = await _httpClient.PostAsJsonAsync(
                "api/internal/notifications/execution-status",
                notification,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Failed to send execution status notification: StatusCode={StatusCode}, ExecutionId={ExecutionId}",
                    response.StatusCode, update.ExecutionId);
            }
            else
            {
                _logger.LogDebug(
                    "Sent execution status notification: ExecutionId={ExecutionId}, Status={Status}",
                    update.ExecutionId, update.Status);
            }
        }
        catch (Exception ex)
        {
            // Don't throw - notification failures shouldn't break pipeline execution
            _logger.LogError(ex,
                "Error sending execution status notification: ExecutionId={ExecutionId}",
                update.ExecutionId);
        }
    }

    public async Task NotifyExecutionProgressAsync(
        Guid tenantId,
        ExecutionProgressUpdate update,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var notification = new
            {
                TenantId = tenantId,
                Update = update
            };

            var response = await _httpClient.PostAsJsonAsync(
                "api/internal/notifications/execution-progress",
                notification,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Failed to send execution progress notification: StatusCode={StatusCode}, ExecutionId={ExecutionId}",
                    response.StatusCode, update.ExecutionId);
            }
            else
            {
                _logger.LogDebug(
                    "Sent execution progress notification: ExecutionId={ExecutionId}, Progress={Progress}%",
                    update.ExecutionId, update.ProgressPercent);
            }
        }
        catch (Exception ex)
        {
            // Don't throw - notification failures shouldn't break pipeline execution
            _logger.LogError(ex,
                "Error sending execution progress notification: ExecutionId={ExecutionId}",
                update.ExecutionId);
        }
    }

    public async Task NotifyExecutionLogAddedAsync(
        Guid tenantId,
        Guid executionId,
        ExecutionLogDto logEntry,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var notification = new
            {
                TenantId = tenantId,
                ExecutionId = executionId,
                LogEntry = logEntry
            };

            var response = await _httpClient.PostAsJsonAsync(
                "api/internal/notifications/execution-log",
                notification,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Failed to send execution log notification: StatusCode={StatusCode}, ExecutionId={ExecutionId}",
                    response.StatusCode, executionId);
            }
            else
            {
                _logger.LogDebug(
                    "Sent execution log notification: ExecutionId={ExecutionId}, Level={Level}",
                    executionId, logEntry.Level);
            }
        }
        catch (Exception ex)
        {
            // Don't throw - notification failures shouldn't break pipeline execution
            _logger.LogError(ex,
                "Error sending execution log notification: ExecutionId={ExecutionId}",
                executionId);
        }
    }
}
