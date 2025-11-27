namespace MultiTenantETL.Application.Executions.Models;

public class ExecutionResponse
{
    public Guid Id { get; set; }
    public Guid PipelineId { get; set; }
    public string? PipelineName { get; set; }
    public Guid TenantId { get; set; }
    public string? TenantName { get; set; }
    
    public required string Status { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public long? Duration { get; set; } // Duration in milliseconds
    
    public int RecordsProcessed { get; set; }
    public int RecordsSucceeded { get; set; }
    public int RecordsFailed { get; set; }
    public decimal ProgressPercent { get; set; }
    
    public string? ErrorMessage { get; set; }
    public List<ExecutionLogDto> Logs { get; set; } = new();
    
    public required string TriggeredBy { get; set; }
    public string? TriggeredByUserEmail { get; set; }
    
    public DateTime CreatedAt { get; set; }
}
