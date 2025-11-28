namespace MultiTenantETL.Application.Connectors.DataReaders;

/// <summary>
/// Result of schema detection
/// </summary>
public class SchemaDetectionResult
{
    public SchemaInfo? Schema { get; set; }
    public bool IsSuccessful { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime DetectedAt { get; set; } = DateTime.UtcNow;
}
