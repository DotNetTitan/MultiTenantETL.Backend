namespace MultiTenantETL.Application.Executions.Models;

public class ExecutionLogDto
{
    public DateTimeOffset Timestamp { get; set; }
    public required string Level { get; set; }
    public required string Source { get; set; }
    public required string Message { get; set; }
    public string? Details { get; set; }
}
