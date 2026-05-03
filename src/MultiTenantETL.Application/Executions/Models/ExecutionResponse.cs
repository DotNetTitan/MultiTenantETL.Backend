namespace MultiTenantETL.Application.Executions.Models;

public class ExecutionResponse
{
    public Guid Id { get; set; }
    public Guid PipelineId { get; set; }
    public string? PipelineName { get; set; }
    public Guid TenantId { get; set; }
    public string? TenantName { get; set; }
    
    public required string Status { get; set; }
    public DateTimeOffset StartTime { get; set; }
    public DateTimeOffset? EndTime { get; set; }
    public TimeSpan? Duration { get; set; }
    public long? DurationMs => Duration.HasValue ? (long)Math.Round(Duration.Value.TotalMilliseconds) : null;
    
    public long RecordsProcessed { get; set; }
    public long RecordsSucceeded { get; set; }
    public long RecordsFailed { get; set; }
    public decimal ProgressPercent { get; set; }
    
    public int BatchCount { get; set; }
    
    public string? ErrorMessage { get; set; }
    public List<ExecutionLogDto> Logs { get; set; } = new();
    
    public required string TriggeredBy { get; set; }
    public string? TriggeredByUserEmail { get; set; }
    
    public DateTimeOffset CreatedAt { get; set; }
}
