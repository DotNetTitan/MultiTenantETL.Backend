using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MultiTenantETL.Application.Messaging;
using MultiTenantETL.Infrastructure.Configuration;
using RabbitMQ.Client;
using System.Text;
using System.Text.Json;

namespace MultiTenantETL.Infrastructure.Messaging;

public class RabbitMqPublisher : IMessagePublisher, IDisposable
{
    private readonly RabbitMqSettings _settings;
    private readonly ILogger<RabbitMqPublisher> _logger;
    private readonly IConnection _connection;
    private readonly IModel _channel;

    public RabbitMqPublisher(
        IOptions<RabbitMqSettings> settings,
        ILogger<RabbitMqPublisher> logger)
    {
        _settings = settings.Value;
        _logger = logger;

        var factory = _settings.CreateConnectionFactory();
        // Publisher doesn't need async dispatch
        factory.DispatchConsumersAsync = false;

        _connection = factory.CreateConnection();
        _channel = _connection.CreateModel();

        // Declare dead letter exchange
        _channel.ExchangeDeclare(
            exchange: _settings.DeadLetterExchange,
            type: ExchangeType.Direct,
            durable: true);

        // Declare execution queue with DLX
        var queueArgs = new Dictionary<string, object>
        {
            { "x-dead-letter-exchange", _settings.DeadLetterExchange }
        };

        _channel.QueueDeclare(
            queue: _settings.ExecutionQueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: queueArgs);

        // Declare cancellation queue
        _channel.QueueDeclare(
            queue: _settings.CancellationQueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null);

        _logger.LogInformation("RabbitMQ publisher initialized");
    }

    public Task PublishExecutionTaskAsync(ExecutionTask task, CancellationToken cancellationToken = default)
    {
        try
        {
            var json = JsonSerializer.Serialize(task);
            var body = Encoding.UTF8.GetBytes(json);

            var properties = _channel.CreateBasicProperties();
            properties.Persistent = true;
            properties.ContentType = "application/json";
            properties.MessageId = task.ExecutionId.ToString();
            properties.Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds());

            _channel.BasicPublish(
                exchange: string.Empty,
                routingKey: _settings.ExecutionQueueName,
                basicProperties: properties,
                body: body);

            _logger.LogInformation(
                "Published execution task for ExecutionId: {ExecutionId}, PipelineId: {PipelineId}",
                task.ExecutionId, task.PipelineId);

            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish execution task for ExecutionId: {ExecutionId}", task.ExecutionId);
            throw;
        }
    }

    public Task PublishCancellationRequestAsync(Guid executionId, CancellationToken cancellationToken = default)
    {
        try
        {
            var message = new { ExecutionId = executionId, CancelledAt = DateTimeOffset.UtcNow };
            var json = JsonSerializer.Serialize(message);
            var body = Encoding.UTF8.GetBytes(json);

            var properties = _channel.CreateBasicProperties();
            properties.Persistent = true;
            properties.ContentType = "application/json";
            properties.MessageId = executionId.ToString();

            _channel.BasicPublish(
                exchange: string.Empty,
                routingKey: _settings.CancellationQueueName,
                basicProperties: properties,
                body: body);

            _logger.LogInformation("Published cancellation request for ExecutionId: {ExecutionId}", executionId);

            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish cancellation request for ExecutionId: {ExecutionId}", executionId);
            throw;
        }
    }

    public void Dispose()
    {
        _channel?.Close();
        _channel?.Dispose();
        _connection?.Close();
        _connection?.Dispose();
    }
}
