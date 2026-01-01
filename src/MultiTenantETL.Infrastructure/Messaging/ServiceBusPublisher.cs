using System.Text;
using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MultiTenantETL.Application.Messaging;
using MultiTenantETL.Infrastructure.Configuration;

namespace MultiTenantETL.Infrastructure.Messaging;

public class ServiceBusPublisher : IMessagePublisher, IAsyncDisposable
{
    private readonly ServiceBusSettings _settings;
    private readonly ILogger<ServiceBusPublisher> _logger;
    private readonly ServiceBusClient _client;
    private readonly ServiceBusSender _executionSender;
    private readonly ServiceBusSender _cancellationSender;

    public ServiceBusPublisher(
        IOptions<ServiceBusSettings> settings,
        ILogger<ServiceBusPublisher> logger)
    {
        _settings = settings.Value;
        _logger = logger;

        if (string.IsNullOrEmpty(_settings.ConnectionString))
        {
            throw new InvalidOperationException("Azure Service Bus connection string is not configured");
        }

        _client = new ServiceBusClient(_settings.ConnectionString);
        _executionSender = _client.CreateSender(_settings.ExecutionQueueName);
        _cancellationSender = _client.CreateSender(_settings.CancellationQueueName);

        _logger.LogInformation("Azure Service Bus publisher initialized");
    }

    public async Task PublishExecutionTaskAsync(ExecutionTask task, CancellationToken cancellationToken = default)
    {
        try
        {
            var json = JsonSerializer.Serialize(task);
            var body = Encoding.UTF8.GetBytes(json);

            var message = new ServiceBusMessage(body)
            {
                ContentType = "application/json",
                MessageId = task.ExecutionId.ToString(),
                Subject = "ExecutionTask",
                ApplicationProperties =
                {
                    ["PipelineId"] = task.PipelineId.ToString(),
                    ["TenantId"] = task.TenantId.ToString(),
                    ["ExecutionId"] = task.ExecutionId.ToString()
                }
            };

            await _executionSender.SendMessageAsync(message, cancellationToken);

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
            var body = Encoding.UTF8.GetBytes(json);

            var serviceBusMessage = new ServiceBusMessage(body)
            {
                ContentType = "application/json",
                MessageId = executionId.ToString(),
                Subject = "CancellationRequest",
                ApplicationProperties =
                {
                    ["ExecutionId"] = executionId.ToString()
                }
            };

            await _cancellationSender.SendMessageAsync(serviceBusMessage, cancellationToken);

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
        await _executionSender.DisposeAsync();
        await _cancellationSender.DisposeAsync();
        await _client.DisposeAsync();
    }
}
