using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Options;
using MultiTenantETL.Application.Common.Interfaces;
using MultiTenantETL.Application.Messaging;
using MultiTenantETL.Application.Orchestration;
using MultiTenantETL.Infrastructure.Configuration;
using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;

namespace MultiTenantETL.Worker;

public class ServiceBusWorker : BackgroundService
{
    private readonly ILogger<ServiceBusWorker> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly ServiceBusSettings _settings;
    private ServiceBusClient? _client;
    private ServiceBusProcessor? _executionProcessor;
    private ServiceBusProcessor? _cancellationProcessor;
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _runningExecutions = new();

    public ServiceBusWorker(
        ILogger<ServiceBusWorker> logger,
        IServiceProvider serviceProvider,
        IOptions<ServiceBusSettings> settings)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _settings = settings.Value;
    }

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Pipeline Worker starting...");

        if (string.IsNullOrEmpty(_settings.ConnectionString))
        {
            throw new InvalidOperationException("Azure Service Bus connection string is not configured");
        }

        _client = new ServiceBusClient(_settings.ConnectionString);

        // Create processor for execution queue
        var executionProcessorOptions = new ServiceBusProcessorOptions
        {
            MaxConcurrentCalls = _settings.MaxConcurrentCalls,
            AutoCompleteMessages = false,
            PrefetchCount = _settings.PrefetchCount,
            MaxAutoLockRenewalDuration = TimeSpan.FromMinutes(5)
        };

        _executionProcessor = _client.CreateProcessor(_settings.ExecutionQueueName, executionProcessorOptions);
        _executionProcessor.ProcessMessageAsync += HandleExecutionTaskAsync;
        _executionProcessor.ProcessErrorAsync += HandleErrorAsync;

        // Create processor for cancellation queue
        var cancellationProcessorOptions = new ServiceBusProcessorOptions
        {
            MaxConcurrentCalls = _settings.MaxConcurrentCalls,
            AutoCompleteMessages = true, // Auto-complete cancellation messages
            PrefetchCount = _settings.PrefetchCount
        };

        _cancellationProcessor = _client.CreateProcessor(_settings.CancellationQueueName, cancellationProcessorOptions);
        _cancellationProcessor.ProcessMessageAsync += HandleCancellationRequestAsync;
        _cancellationProcessor.ProcessErrorAsync += HandleErrorAsync;

        _logger.LogInformation("Connected to Azure Service Bus");
        _logger.LogInformation("Queues configured: {ExecutionQueue}, {CancellationQueue}",
            _settings.ExecutionQueueName, _settings.CancellationQueueName);

        return base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_executionProcessor == null || _cancellationProcessor == null)
        {
            _logger.LogError("Service Bus processors are not initialized");
            return;
        }

        // Start processing messages
        await _executionProcessor.StartProcessingAsync(stoppingToken);
        await _cancellationProcessor.StartProcessingAsync(stoppingToken);

        _logger.LogInformation("Worker is now listening for execution tasks...");

        // Keep the worker running
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(1000, stoppingToken);
        }
    }

    private async Task HandleExecutionTaskAsync(ProcessMessageEventArgs args)
    {
        var body = args.Message.Body.ToArray();
        var json = Encoding.UTF8.GetString(body);

        ExecutionTask? task = null;

        try
        {
            task = JsonSerializer.Deserialize<ExecutionTask>(json);

            if (task == null)
            {
                _logger.LogError("Failed to deserialize execution task");
                await args.DeadLetterMessageAsync(args.Message, "DeserializationFailed", "Failed to deserialize message body");
                return;
            }

            // Validate tenant ID is present
            if (task.TenantId == Guid.Empty)
            {
                _logger.LogError(
                    "Execution task missing TenantId: ExecutionId={ExecutionId}, PipelineId={PipelineId}",
                    task.ExecutionId, task.PipelineId);
                await args.DeadLetterMessageAsync(args.Message, "MissingTenantId", "Execution task is missing TenantId");
                return;
            }

            _logger.LogInformation(
                "Received execution task: ExecutionId={ExecutionId}, PipelineId={PipelineId}, TenantId={TenantId}",
                task.ExecutionId, task.PipelineId, task.TenantId);

            // Create cancellation token source for this execution
            var cts = CancellationTokenSource.CreateLinkedTokenSource(args.CancellationToken);
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

            // Complete the message
            await args.CompleteMessageAsync(args.Message);

            _logger.LogInformation("Execution task completed: ExecutionId={ExecutionId}", task.ExecutionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing execution task: ExecutionId={ExecutionId}", task?.ExecutionId);

            if (task != null)
            {
                CompleteExecution(task.ExecutionId);
            }

            // Don't retry - just fail and move to dead letter queue
            // Retrying would cause duplicate batch records and infinite loops
            _logger.LogError("Execution task failed, sending to dead letter queue: ExecutionId={ExecutionId}", task?.ExecutionId);

            try
            {
                await args.DeadLetterMessageAsync(args.Message, "ExecutionFailed", ex.Message);
            }
            catch (Exception dlqEx)
            {
                _logger.LogError(dlqEx, "Failed to dead letter message for ExecutionId={ExecutionId}", task?.ExecutionId);
                // If we can't dead letter, abandon the message
                await args.AbandonMessageAsync(args.Message);
            }
        }
    }

    private async Task HandleCancellationRequestAsync(ProcessMessageEventArgs args)
    {
        var body = args.Message.Body.ToArray();
        var json = Encoding.UTF8.GetString(body);

        try
        {
            var request = JsonSerializer.Deserialize<CancellationRequest>(json);

            if (request != null)
            {
                if (CancelExecution(request.ExecutionId))
                {
                    _logger.LogInformation("Cancelling execution: ExecutionId={ExecutionId}", request.ExecutionId);
                }
                else
                {
                    _logger.LogWarning("Cancellation requested for non-running execution: ExecutionId={ExecutionId}",
                        request.ExecutionId);
                }
            }
            else
            {
                _logger.LogError("Failed to deserialize cancellation request");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing cancellation request");
        }
    }

    private Task HandleErrorAsync(ProcessErrorEventArgs args)
    {
        _logger.LogError(args.Exception,
            "Error processing message from {EntityPath}: {ErrorSource}",
            args.EntityPath, args.ErrorSource);
        return Task.CompletedTask;
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

        // Stop processors first
        if (_executionProcessor != null)
        {
            await _executionProcessor.StopProcessingAsync(cancellationToken);
        }

        if (_cancellationProcessor != null)
        {
            await _cancellationProcessor.StopProcessingAsync(cancellationToken);
        }

        // Dispose processors and client
        if (_executionProcessor != null)
        {
            await _executionProcessor.DisposeAsync();
            _executionProcessor = null;
        }

        if (_cancellationProcessor != null)
        {
            await _cancellationProcessor.DisposeAsync();
            _cancellationProcessor = null;
        }

        if (_client != null)
        {
            await _client.DisposeAsync();
            _client = null;
        }

        await base.StopAsync(cancellationToken);
    }

    public override void Dispose()
    {
        // Dispose synchronous resources
        foreach (var execution in _runningExecutions.ToArray())
        {
            if (_runningExecutions.TryRemove(execution.Key, out var cts))
            {
                cts.Dispose();
            }
        }
        _runningExecutions.Clear();

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
