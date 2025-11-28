namespace MultiTenantETL.Application.Connectors.DataReaders;

/// <summary>
/// Result of a data read operation
/// </summary>
public class DataReadResult
{
    public List<Dictionary<string, object?>> Rows { get; set; } = new();
    public int TotalRows { get; set; }
    public SchemaInfo? Schema { get; set; }
    public List<string> Warnings { get; set; } = new();
}
