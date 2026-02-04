namespace MultiTenantETL.Application.Executions.Models;

public class ExecutionProgressUpdate
{
    public Guid ExecutionId { get; set; }
    public long RecordsProcessed { get; set; }
    public long RecordsSucceeded { get; set; }
    public long RecordsFailed { get; set; }
    public int ProgressPercent { get; set; }
    public int BatchCount { get; set; }
}
