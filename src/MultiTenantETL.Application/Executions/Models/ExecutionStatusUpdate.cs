namespace MultiTenantETL.Application.Executions.Models;

public class ExecutionStatusUpdate
{
    public Guid ExecutionId { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset? EndTime { get; set; }
    public TimeSpan? Duration { get; set; }
    public string? ErrorMessage { get; set; }
}
