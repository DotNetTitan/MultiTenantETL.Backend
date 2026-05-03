namespace MultiTenantETL.Application.Executions.Models;

/// <summary>
/// Request model for searching and filtering pipeline executions with pagination.
/// </summary>
public class ExecutionSearchRequest
{
    public Guid? PipelineId { get; set; }
    public string? Status { get; set; }
    public string? TriggeredBy { get; set; }
    public DateTimeOffset? StartDate { get; set; }
    public DateTimeOffset? EndDate { get; set; }
    public string? Search { get; set; }
    
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? SortBy { get; set; } = "startTime_desc";
}
