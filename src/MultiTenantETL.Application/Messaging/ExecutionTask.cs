namespace MultiTenantETL.Application.Messaging;

/// <summary>
/// Message contract for pipeline execution tasks sent to the message broker
/// </summary>
public class ExecutionTask
{
    public Guid ExecutionId { get; set; }
    public Guid PipelineId { get; set; }
    public Guid TenantId { get; set; }
    public int BatchSize { get; set; } = 1000;
    public bool DryRun { get; set; }
    public Dictionary<string, string> Options { get; set; } = new();
    public DateTimeOffset QueuedAt { get; set; }
}

/// <summary>
/// Message contract for pipeline cancellation requests
/// </summary>
public class CancellationRequest
{
    public Guid ExecutionId { get; set; }
    public DateTimeOffset CancelledAt { get; set; }
}
