namespace MultiTenantETL.Application.Connectors.DataWriters;

/// <summary>
/// Options for configuring a data write operation.
/// </summary>
public class WriteOptions
{
    public bool TruncateBeforeLoad { get; set; }
    public bool UseUpsert { get; set; }
    public List<string>? UpsertKeys { get; set; }
    public Dictionary<string, object>? Parameters { get; set; }
}
