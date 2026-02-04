namespace MultiTenantETL.Application.Executions;

/// <summary>
/// Service for broadcasting execution log entries in real-time
/// </summary>
public interface IExecutionLogBroadcaster
{
    /// <summary>
    /// Broadcasts a log entry to all clients subscribed to the execution
    /// </summary>
    Task BroadcastLogEntryAsync(Guid executionId, ExecutionLogBroadcastDto logEntry, CancellationToken cancellationToken = default);

    /// <summary>
    /// Broadcasts an execution status update to all clients subscribed to the execution
    /// </summary>
    Task BroadcastExecutionStatusAsync(Guid executionId, ExecutionStatusBroadcastDto status, CancellationToken cancellationToken = default);
}

/// <summary>
/// DTO for broadcasting log entries
/// </summary>
public class ExecutionLogBroadcastDto
{
    public Guid Id { get; set; }
    public Guid ExecutionId { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    public required string Level { get; set; }
    public required string Source { get; set; }
    public required string Message { get; set; }
    public string? Details { get; set; }
    public Guid? BatchId { get; set; }
}

/// <summary>
/// DTO for broadcasting execution status updates
/// </summary>
public class ExecutionStatusBroadcastDto
{
    public Guid ExecutionId { get; set; }
    public required string Status { get; set; }
    public long RecordsProcessed { get; set; }
    public long RecordsSucceeded { get; set; }
    public long RecordsFailed { get; set; }
    public decimal ProgressPercent { get; set; }
    public int BatchCount { get; set; }
}
