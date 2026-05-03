namespace MultiTenantETL.Application.Executions.Models;

/// <summary>
/// Paginated response containing execution list with pagination metadata.
/// </summary>
public class PagedExecutionResponse
{
    public List<ExecutionListResponse> Executions { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
}
