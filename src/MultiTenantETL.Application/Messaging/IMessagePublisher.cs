namespace MultiTenantETL.Application.Messaging;

/// <summary>
/// Interface for publishing messages to the message broker
/// </summary>
public interface IMessagePublisher
{
    /// <summary>
    /// Publishes an execution task to the queue
    /// </summary>
    Task PublishExecutionTaskAsync(ExecutionTask task, CancellationToken cancellationToken = default);

    /// <summary>
    /// Publishes a cancellation request for an execution
    /// </summary>
    Task PublishCancellationRequestAsync(Guid executionId, CancellationToken cancellationToken = default);
}
