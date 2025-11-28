namespace MultiTenantETL.Application.Connectors.DataReaders;

public class ReadOptions
{
    public int BatchSize { get; set; } = 1000;
    public int? MaxRows { get; set; }
    public Dictionary<string, object>? Parameters { get; set; }
}
