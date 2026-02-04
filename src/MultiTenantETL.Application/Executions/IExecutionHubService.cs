using MultiTenantETL.Application.Executions.Models;

namespace MultiTenantETL.Application.Executions;

/// <summary>
/// Service for sending real-time execution updates via SignalR
/// </summary>
public interface IExecutionHubService
{
    /// <summary>
    /// Send log entry to specific execution subscribers
    /// </summary>
    Task SendLogAsync(Guid executionId, Guid tenantId, ExecutionLogDto log, CancellationToken cancellationToken = default);

    /// <summary>
    /// Send execution status update to specific execution subscribers
    /// </summary>
    Task SendStatusUpdateAsync(Guid executionId, Guid tenantId, ExecutionStatusUpdate update, CancellationToken cancellationToken = default);

    /// <summary>
    /// Send execution statistics update to specific execution subscribers
    /// </summary>
    Task SendStatsUpdateAsync(Guid executionId, Guid tenantId, ExecutionProgressUpdate stats, CancellationToken cancellationToken = default);

    /// <summary>
    /// Send completion notification to specific execution subscribers
    /// </summary>
    Task SendCompletionAsync(Guid executionId, Guid tenantId, ExecutionCompletionUpdate completion, CancellationToken cancellationToken = default);
}

/// <summary>
/// Execution status update for SignalR
/// </summary>
public class ExecutionStatusUpdate
{
    public required string Status { get; set; }
    public DateTimeOffset Timestamp { get; set; }
}

/// <summary>
/// Execution progress/stats update for SignalR
/// </summary>
public class ExecutionProgressUpdate
{
    public long RecordsProcessed { get; set; }
    public long RecordsSucceeded { get; set; }
    public long RecordsFailed { get; set; }
    public decimal ProgressPercent { get; set; }
    public int BatchCount { get; set; }
    public DateTimeOffset Timestamp { get; set; }
}

/// <summary>
/// Execution completion update for SignalR
/// </summary>
public class ExecutionCompletionUpdate
{
    public required string Status { get; set; }
    public DateTimeOffset StartTime { get; set; }
    public DateTimeOffset EndTime { get; set; }
    public TimeSpan Duration { get; set; }
    public long RecordsProcessed { get; set; }
    public long RecordsSucceeded { get; set; }
    public long RecordsFailed { get; set; }
    public string? ErrorMessage { get; set; }
}
