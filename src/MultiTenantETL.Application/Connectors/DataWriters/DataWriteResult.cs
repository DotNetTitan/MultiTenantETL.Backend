namespace MultiTenantETL.Application.Connectors.DataWriters;

public class DataWriteResult
{
    public Guid BatchId { get; set; }
    public int RowsWritten { get; set; }
    public int RowsFailed { get; set; }
    public List<string> Errors { get; set; } = new();
    public Dictionary<string, object>? Metadata { get; set; }
}
