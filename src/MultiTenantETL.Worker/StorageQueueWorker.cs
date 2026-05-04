using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;
using Microsoft.Extensions.Options;
using MultiTenantETL.Application.Common.Interfaces;
using MultiTenantETL.Application.Messaging;
using MultiTenantETL.Application.Orchestration;
using MultiTenantETL.Infrastructure.Configuration;
using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;

namespace MultiTenantETL.Worker;

public class StorageQueueWorker : BackgroundService
{
    private readonly ILogger<StorageQueueWorker> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly StorageQueueSettings _settings;
    private QueueClient? _executionClient;
    private QueueClient? _cancellationClient;
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _runningExecutions = new();

    public StorageQueueWorker(
        ILogger<StorageQueueWorker> logger,
        IServiceProvider serviceProvider,
        IOptions<StorageQueueSettings> settings)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _settings = settings.Value;
    }

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Pipeline Worker (Storage Queue) starting...");

        if (string.IsNullOrEmpty(_settings.ConnectionString))
        {
            throw new InvalidOperationException("Azure Storage connection string is not configured");
        }

        try
        {
            _executionClient = new QueueClient(_settings.ConnectionString, _settings.ExecutionQueueName);
            _cancellationClient = new QueueClient(_settings.ConnectionString, _settings.CancellationQueueName);

            // Ensure queues exist
            _executionClient.CreateIfNotExists();
            _cancellationClient.CreateIfNotExists();

            _logger.LogInformation("Connected to Azure Storage Queues");
            _logger.LogInformation("Queues configured: {ExecutionQueue}, {CancellationQueue}",
                _settings.ExecutionQueueName, _settings.CancellationQueueName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize Storage Queue clients");
            throw;
        }

        return base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_executionClient == null || _cancellationClient == null)
        {
            _logger.LogError("Storage Queue clients are not initialized");
            return;
        }

        _logger.LogInformation("Worker is now listening for execution tasks...");

        // Poll both queues concurrently
        var executionTask = PollExecutionQueueAsync(stoppingToken);
        var cancellationTask = PollCancellationQueueAsync(stoppingToken);

        try
        {
            await Task.WhenAll(executionTask, cancellationTask);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Worker polling cancelled");
        }
    }

    private async Task PollExecutionQueueAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var messages = await _executionClient!.ReceiveMessagesAsync(
                    maxMessages: _settings.MaxConcurrentReceives,
                    visibilityTimeout: _settings.MessageVisibilityTimeout,
                    cancellationToken: cancellationToken);

                if (messages.Value.Any())
                {
                    foreach (var message in messages.Value)
                    {
                        _ = HandleExecutionTaskAsync(message);
                    }
                }
                else
                {
                    await Task.Delay(_settings.PollingIntervalMs, cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error polling execution queue");
                await Task.Delay(_settings.PollingIntervalMs, cancellationToken);
            }
        }
    }

    private async Task PollCancellationQueueAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var messages = await _cancellationClient!.ReceiveMessagesAsync(
                    maxMessages: _settings.MaxConcurrentReceives,
                    visibilityTimeout: _settings.MessageVisibilityTimeout,
                    cancellationToken: cancellationToken);

                if (messages.Value.Any())
                {
                    foreach (var message in messages.Value)
                    {
                        _ = HandleCancellationRequestAsync(message);
                    }
                }
                else
                {
                    await Task.Delay(_settings.PollingIntervalMs, cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error polling cancellation queue");
                await Task.Delay(_settings.PollingIntervalMs, cancellationToken);
            }
        }
    }

    private async Task HandleExecutionTaskAsync(QueueMessage message)
    {
        ExecutionTask? task = null;

        try
        {
            var json = Encoding.UTF8.GetString(Convert.FromBase64String(message.Body.ToString()));
            task = JsonSerializer.Deserialize<ExecutionTask>(json);

            if (task == null)
            {
                _logger.LogError("Failed to deserialize execution task");
                await _executionClient!.DeleteMessageAsync(message.MessageId, message.PopReceipt);
                return;
            }

            // Validate tenant ID is present
            if (task.TenantId == Guid.Empty)
            {
                _logger.LogError(
                    "Execution task missing TenantId: ExecutionId={ExecutionId}, PipelineId={PipelineId}",
                    task.ExecutionId, task.PipelineId);
                await _executionClient!.DeleteMessageAsync(message.MessageId, message.PopReceipt);
                return;
            }

            _logger.LogInformation(
                "Received execution task: ExecutionId={ExecutionId}, PipelineId={PipelineId}, TenantId={TenantId}",
                task.ExecutionId, task.PipelineId, task.TenantId);

            // Create cancellation token source for this execution
            var cts = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken.None);
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

            // Delete the message
            await _executionClient!.DeleteMessageAsync(message.MessageId, message.PopReceipt);

            _logger.LogInformation("Execution task completed: ExecutionId={ExecutionId}", task.ExecutionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing execution task: ExecutionId={ExecutionId}", task?.ExecutionId);

            if (task != null)
            {
                CompleteExecution(task.ExecutionId);
            }

            // Send to poison queue by deleting from main queue after max retries
            try
            {
                // Let the message visibility timeout reset for retry
                // After MaxDequeueCount retries, it will be moved to poison queue by Storage Queue
                int dequeueCount = GetDequeueCount(message);
                if (dequeueCount >= _settings.MaxDequeueCount)
                {
                    _logger.LogError("Execution task failed after {MaxRetries} retries, deleting from queue: ExecutionId={ExecutionId}",
                        _settings.MaxDequeueCount, task?.ExecutionId);
                    await _executionClient!.DeleteMessageAsync(message.MessageId, message.PopReceipt);
                }
                else
                {
                    // Visibility will reset on next poll, allowing retry
                    _logger.LogWarning("Execution task failed, will retry (attempt {DequeueCount}/{MaxRetries}): ExecutionId={ExecutionId}",
                        dequeueCount, _settings.MaxDequeueCount, task?.ExecutionId);
                    await _executionClient!.UpdateMessageAsync(message.MessageId, message.PopReceipt, message.Body, visibilityTimeout: TimeSpan.Zero);
                }
            }
            catch (Exception dlqEx)
            {
                _logger.LogError(dlqEx, "Failed to handle failed message for ExecutionId={ExecutionId}", task?.ExecutionId);
            }
        }
    }

    private async Task HandleCancellationRequestAsync(QueueMessage message)
    {
        try
        {
            var json = Encoding.UTF8.GetString(Convert.FromBase64String(message.Body.ToString()));
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

            // Delete the message
            await _cancellationClient!.DeleteMessageAsync(message.MessageId, message.PopReceipt);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing cancellation request");
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Pipeline Worker (Storage Queue) stopping...");

        // Cancel all running executions
        foreach (var execution in _runningExecutions.ToArray())
        {
            if (_runningExecutions.TryRemove(execution.Key, out var cts))
            {
                cts.Cancel();
                cts.Dispose();
            }
        }

        // Note: QueueClient doesn't require explicit cleanup

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

    private int GetDequeueCount(QueueMessage message)
    {
        // Azure Storage Queue tracks dequeue count in message metadata
        // DequeueCount is a long, not nullable
        return (int)message.DequeueCount;
    }
}
