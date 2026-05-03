namespace MultiTenantETL.Application.Executions.Models;

/// <summary>
/// Request model to start a new pipeline execution.
/// </summary>
public class StartExecutionRequest
{
    public Guid PipelineId { get; set; }
    public string TriggeredBy { get; set; } = "Manual";
}
