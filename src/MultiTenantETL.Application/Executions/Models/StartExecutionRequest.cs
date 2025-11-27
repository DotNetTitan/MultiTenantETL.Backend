namespace MultiTenantETL.Application.Executions.Models;

public class StartExecutionRequest
{
    public Guid PipelineId { get; set; }
    public string TriggeredBy { get; set; } = "Manual";
}
