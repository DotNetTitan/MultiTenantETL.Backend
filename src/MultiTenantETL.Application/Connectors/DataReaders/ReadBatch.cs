namespace MultiTenantETL.Application.Connectors.DataReaders;

/// <summary>
/// Represents a batch of rows read from a data source.
/// </summary>
public class ReadBatch
{
    public Guid BatchId { get; set; }
    public List<Dictionary<string, object?>> Rows { get; set; } = new();
    public int RowCount { get; set; }
    public Dictionary<string, object>? Metadata { get; set; }
}
