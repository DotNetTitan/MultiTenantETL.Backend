using Azure.Storage.Queues;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MultiTenantETL.Application.Messaging;
using MultiTenantETL.Infrastructure.Configuration;
using System.Text;
using System.Text.Json;

namespace MultiTenantETL.Infrastructure.Messaging;

public class StorageQueuePublisher : IMessagePublisher, IAsyncDisposable
{
    private readonly StorageQueueSettings _settings;
    private readonly ILogger<StorageQueuePublisher> _logger;
    private readonly QueueClient _executionClient;
    private readonly QueueClient _cancellationClient;

    public StorageQueuePublisher(
        IOptions<StorageQueueSettings> settings,
        ILogger<StorageQueuePublisher> logger)
    {
        _settings = settings.Value;
        _logger = logger;

        if (string.IsNullOrEmpty(_settings.ConnectionString))
        {
            throw new InvalidOperationException("Azure Storage connection string is not configured");
        }

        _executionClient = new QueueClient(_settings.ConnectionString, _settings.ExecutionQueueName);
        _cancellationClient = new QueueClient(_settings.ConnectionString, _settings.CancellationQueueName);

        // Ensure queues exist
        try
        {
            _executionClient.CreateIfNotExists();
            _cancellationClient.CreateIfNotExists();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to create queues, they may already exist");
        }

        _logger.LogInformation("Azure Storage Queue publisher initialized");
    }

    public async Task PublishExecutionTaskAsync(ExecutionTask task, CancellationToken cancellationToken = default)
    {
        try
        {
            var json = JsonSerializer.Serialize(task);
            var encodedMessage = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));

            await _executionClient.SendMessageAsync(encodedMessage, cancellationToken: cancellationToken);

            _logger.LogInformation(
                "Published execution task for ExecutionId: {ExecutionId}, PipelineId: {PipelineId}",
                task.ExecutionId, task.PipelineId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish execution task for ExecutionId: {ExecutionId}", task.ExecutionId);
            throw;
        }
    }

    public async Task PublishCancellationRequestAsync(Guid executionId, CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new CancellationRequest
            {
                ExecutionId = executionId,
                CancelledAt = DateTimeOffset.UtcNow
            };
            var json = JsonSerializer.Serialize(request);
            var encodedMessage = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));

            await _cancellationClient.SendMessageAsync(encodedMessage, cancellationToken: cancellationToken);

            _logger.LogInformation("Published cancellation request for ExecutionId: {ExecutionId}", executionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish cancellation request for ExecutionId: {ExecutionId}", executionId);
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        // QueueClient doesn't require explicit cleanup
        await ValueTask.CompletedTask;
    }
}
