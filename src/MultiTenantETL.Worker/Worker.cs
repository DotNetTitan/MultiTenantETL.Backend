using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using MultiTenantETL.Application.Common.Interfaces;
using MultiTenantETL.Application.Messaging;
using MultiTenantETL.Application.Orchestration;
using MultiTenantETL.Infrastructure.Configuration;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace MultiTenantETL.Worker;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly RabbitMqSettings _settings;
    private IConnection? _connection;
    private IModel? _channel;
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _runningExecutions = new();

    public Worker(
        ILogger<Worker> logger,
        IServiceProvider serviceProvider,
        IOptions<RabbitMqSettings> settings)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _settings = settings.Value;
    }

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Pipeline Worker starting...");
        
        var factory = _settings.CreateConnectionFactory();

        _connection = factory.CreateConnection();
        _channel = _connection.CreateModel();

        // Declare dead letter exchange
        _channel.ExchangeDeclare(
            exchange: _settings.DeadLetterExchange,
            type: ExchangeType.Direct,
            durable: true);

        // Declare queues (idempotent - won't fail if they already exist)
        // Must match the arguments used in RabbitMqPublisher
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

        _channel.QueueDeclare(
            queue: _settings.CancellationQueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null);

        // Set prefetch count to limit concurrent executions
        _channel.BasicQos(prefetchSize: 0, prefetchCount: (ushort)_settings.PrefetchCount, global: false);

        _logger.LogInformation("Connected to RabbitMQ at {HostName}:{Port}", _settings.HostName, _settings.Port);
        _logger.LogInformation("Queues declared: {ExecutionQueue}, {CancellationQueue}", 
            _settings.ExecutionQueueName, _settings.CancellationQueueName);

        return base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_channel == null)
        {
            _logger.LogError("RabbitMQ channel is not initialized");
            return;
        }

        // Start execution consumer
        var executionConsumer = new AsyncEventingBasicConsumer(_channel);
        executionConsumer.Received += async (sender, ea) =>
        {
            await HandleExecutionTask(ea, stoppingToken);
        };

        _channel.BasicConsume(
            queue: _settings.ExecutionQueueName,
            autoAck: false,
            consumer: executionConsumer);

        // Start cancellation consumer
        var cancellationConsumer = new AsyncEventingBasicConsumer(_channel);
        cancellationConsumer.Received += async (sender, ea) =>
        {
            await HandleCancellationRequest(ea);
        };

        _channel.BasicConsume(
            queue: _settings.CancellationQueueName,
            autoAck: true,
            consumer: cancellationConsumer);

        _logger.LogInformation("Worker is now listening for execution tasks...");

        // Keep the worker running
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(1000, stoppingToken);
        }
    }

    private async Task HandleExecutionTask(BasicDeliverEventArgs ea, CancellationToken stoppingToken)
    {
        var body = ea.Body.ToArray();
        var json = Encoding.UTF8.GetString(body);
        
        ExecutionTask? task = null;
        
        try
        {
            task = JsonSerializer.Deserialize<ExecutionTask>(json);
            
            if (task == null)
            {
                _logger.LogError("Failed to deserialize execution task");
                _channel?.BasicNack(ea.DeliveryTag, false, false);
                return;
            }

            // Validate tenant ID is present
            if (task.TenantId == Guid.Empty)
            {
                _logger.LogError(
                    "Execution task missing TenantId: ExecutionId={ExecutionId}, PipelineId={PipelineId}",
                    task.ExecutionId, task.PipelineId);
                _channel?.BasicNack(ea.DeliveryTag, false, false);
                return;
            }

            _logger.LogInformation(
                "Received execution task: ExecutionId={ExecutionId}, PipelineId={PipelineId}, TenantId={TenantId}",
                task.ExecutionId, task.PipelineId, task.TenantId);

            // Create cancellation token source for this execution
            var cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            RegisterExecution(task.ExecutionId, cts);

            // Execute pipeline in a new scope with tenant context
            var scope = _serviceProvider.CreateAsyncScope();
            
            try
            {
                // Set tenant context for this job scope
                var tenantProvider = scope.ServiceProvider.GetRequiredService<ITenantProvider>();
                tenantProvider.TenantId = task.TenantId;
                tenantProvider.CorrelationId = task.ExecutionId.ToString();

                // Add structured logging scope for tenant and correlation
                using (_logger.BeginScope(new Dictionary<string, object>
                {
                    ["TenantId"] = task.TenantId,
                    ["ExecutionId"] = task.ExecutionId,
                    ["CorrelationId"] = tenantProvider.CorrelationId
                }))
                {
                    var orchestrator = scope.ServiceProvider.GetRequiredService<IPipelineOrchestrator>();
                    await orchestrator.ExecutePipelineAsync(task.ExecutionId, cts.Token);
                }
            }
            finally
            {
                await scope.DisposeAsync();
            }

            // Remove from running executions
            CompleteExecution(task.ExecutionId);

            // Acknowledge message
            _channel?.BasicAck(ea.DeliveryTag, false);
            
            _logger.LogInformation("Execution task completed: ExecutionId={ExecutionId}", task.ExecutionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing execution task: ExecutionId={ExecutionId}", task?.ExecutionId);
            
            if (task != null)
            {
                CompleteExecution(task.ExecutionId);
            }

            // Don't retry - just fail and move on
            // Retrying would cause duplicate batch records and infinite loops
            _logger.LogError("Execution task failed, sending to DLX: ExecutionId={ExecutionId}", task?.ExecutionId);
            
            // Nack without requeue (sends to dead letter exchange)
            _channel?.BasicNack(ea.DeliveryTag, false, false);
        }
    }

    private Task HandleCancellationRequest(BasicDeliverEventArgs ea)
    {
        var body = ea.Body.ToArray();
        var json = Encoding.UTF8.GetString(body);
        
        try
        {
            var message = JsonSerializer.Deserialize<CancellationRequest>(json);
            
            if (message != null)
            {
                if (CancelExecution(message.ExecutionId))
                {
                    _logger.LogInformation("Cancelling execution: ExecutionId={ExecutionId}", message.ExecutionId);
                }
                else
                {
                    _logger.LogWarning("Cancellation requested for non-running execution: ExecutionId={ExecutionId}", 
                        message.ExecutionId);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing cancellation request");
        }

        return Task.CompletedTask;
    }

    private int GetRetryCount(IBasicProperties? properties)
    {
        if (properties?.Headers != null && properties.Headers.TryGetValue("x-retry-count", out var value))
        {
            return Convert.ToInt32(value);
        }
        return 0;
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Pipeline Worker stopping...");

        // Cancel all running executions
        foreach (var execution in _runningExecutions.ToArray())
        {
            if (_runningExecutions.TryRemove(execution.Key, out var cts))
            {
                cts.Cancel();
                cts.Dispose();
            }
        }

        _channel?.Close();
        _connection?.Close();

        await base.StopAsync(cancellationToken);
    }

    public override void Dispose()
    {
        _channel?.Dispose();
        _connection?.Dispose();
        
        foreach (var execution in _runningExecutions.ToArray())
        {
            if (_runningExecutions.TryRemove(execution.Key, out var cts))
            {
                cts.Dispose();
            }
        }
        
        base.Dispose();
    }

    private void RegisterExecution(Guid executionId, CancellationTokenSource cts)
    {
        if (_runningExecutions.TryGetValue(executionId, out var existing))
        {
            existing.Cancel();
            existing.Dispose();
        }

        _runningExecutions[executionId] = cts;
    }

    private void CompleteExecution(Guid executionId)
    {
        if (_runningExecutions.TryRemove(executionId, out var cts))
        {
            cts.Dispose();
        }
    }

    private bool CancelExecution(Guid executionId)
    {
        if (_runningExecutions.TryRemove(executionId, out var cts))
        {
            cts.Cancel();
            cts.Dispose();
            return true;
        }

        return false;
    }
}
