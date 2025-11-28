namespace MultiTenantETL.Application.Connectors.DataWriters;

/// <summary>
/// Result of a data write operation
/// </summary>
public class DataWriteResult
{
    public int RowsWritten { get; set; }
    public int RowsFailed { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}
