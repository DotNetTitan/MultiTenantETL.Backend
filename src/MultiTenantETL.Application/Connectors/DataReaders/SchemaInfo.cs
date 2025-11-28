namespace MultiTenantETL.Application.Connectors.DataReaders;

/// <summary>
/// Schema information for a data source
/// </summary>
public class SchemaInfo
{
    public List<FieldDefinition> Fields { get; set; } = new();
    public int Version { get; set; } = 1;
    public DateTime DetectedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Definition of a single field in the schema
/// </summary>
public class FieldDefinition
{
    public string Name { get; set; } = string.Empty;
    public string DataType { get; set; } = string.Empty;
    public bool IsNullable { get; set; }
    public int? MaxLength { get; set; }
    public string? Description { get; set; }
}
