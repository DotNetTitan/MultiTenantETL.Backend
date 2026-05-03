namespace MultiTenantETL.Application.Connectors.DataReaders;

/// <summary>
/// Result of schema detection from a data source connector.
/// </summary>
public class SchemaDetectionResult
{
    public List<FieldDefinition> Fields { get; set; } = new();
    public int Version { get; set; }
    public DateTimeOffset DetectedAt { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Represents a field definition within a detected schema.
/// </summary>
public class FieldDefinition
{
    public required string Name { get; set; }
    public required string DataType { get; set; }
    public bool IsNullable { get; set; }
    public int? MaxLength { get; set; }
    public bool IsPrimaryKey { get; set; }
}
