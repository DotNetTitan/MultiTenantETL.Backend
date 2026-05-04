namespace MultiTenantETL.Application.Executions.Models;

/// <summary>
/// Summary response model for a pipeline execution in lists and search results.
/// </summary>
public class ExecutionListResponse
{
    public Guid Id { get; set; }
    public Guid PipelineId { get; set; }
    public string? PipelineName { get; set; }

    public required string Status { get; set; }
    public DateTimeOffset StartTime { get; set; }
    public DateTimeOffset? EndTime { get; set; }
    public TimeSpan? Duration { get; set; }
    public long? DurationMs => Duration.HasValue ? (long)Math.Round(Duration.Value.TotalMilliseconds) : null;

    public long RecordsProcessed { get; set; }
    public decimal ProgressPercent { get; set; }

    public required string TriggeredBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
