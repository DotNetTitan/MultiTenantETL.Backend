namespace MultiTenantETL.Application.Executions.Models;

/// <summary>
/// Aggregated statistics for pipeline executions within a tenant.
/// </summary>
public class ExecutionStatsDto
{
    public int TotalExecutions { get; set; }
    public int RunningExecutions { get; set; }
    public int CompletedExecutions { get; set; }
    public int FailedExecutions { get; set; }
    public int CancelledExecutions { get; set; }

    public decimal SuccessRate { get; set; }
    public TimeSpan? AverageDuration { get; set; }
    public long? AverageDurationMs => AverageDuration.HasValue ? (long)Math.Round(AverageDuration.Value.TotalMilliseconds) : null;
    public long TotalRecordsProcessed { get; set; }

    public DateTimeOffset? LastExecutionTime { get; set; }
}
