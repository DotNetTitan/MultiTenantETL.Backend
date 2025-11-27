namespace MultiTenantETL.Application.Executions.Models;

public class ExecutionLogDto
{
    public DateTime Timestamp { get; set; }
    public required string Level { get; set; }
    public required string Message { get; set; }
    public string? Details { get; set; }
}
