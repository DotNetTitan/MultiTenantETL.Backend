namespace MultiTenantETL.Application.Connectors.DataReaders;

/// <summary>
/// Options for configuring a data read operation.
/// </summary>
public class ReadOptions
{
    public int BatchSize { get; set; } = 1000;
    public int? MaxRows { get; set; }
    public Dictionary<string, object>? Parameters { get; set; }
}
