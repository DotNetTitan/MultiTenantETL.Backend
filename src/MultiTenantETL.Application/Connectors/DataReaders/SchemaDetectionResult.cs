namespace MultiTenantETL.Application.Connectors.DataReaders;

public class SchemaDetectionResult
{
    public List<FieldDefinition> Fields { get; set; } = new();
    public int Version { get; set; }
    public DateTimeOffset DetectedAt { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
}

public class FieldDefinition
{
    public required string Name { get; set; }
    public required string DataType { get; set; }
    public bool IsNullable { get; set; }
    public int? MaxLength { get; set; }
    public bool IsPrimaryKey { get; set; }
}
