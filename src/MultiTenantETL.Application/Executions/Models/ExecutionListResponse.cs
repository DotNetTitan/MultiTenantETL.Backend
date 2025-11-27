namespace MultiTenantETL.Application.Executions.Models;

public class ExecutionListResponse
{
    public Guid Id { get; set; }
    public Guid PipelineId { get; set; }
    public string? PipelineName { get; set; }
    
    public required string Status { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public long? Duration { get; set; }
    
    public int RecordsProcessed { get; set; }
    public decimal ProgressPercent { get; set; }
    
    public required string TriggeredBy { get; set; }
    public DateTime CreatedAt { get; set; }
}
