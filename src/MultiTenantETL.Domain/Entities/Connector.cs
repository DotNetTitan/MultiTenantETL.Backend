using MultiTenantETL.Domain.Interfaces;

namespace MultiTenantETL.Domain.Entities;

/// <summary>
/// Represents a data source or destination for ETL pipelines.
/// </summary>
public class Connector : ITenantResource
{
    /// <summary>
    /// Unique identifier for this connector.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Tenant that owns this connector.
    /// </summary>
    public Guid TenantId { get; set; }

    /// <summary>
    /// Display name of the connector.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Optional description of the connector.
    /// </summary>
    public string? Description { get; set; }
    
    // Relational columns for querying/filtering
    
    /// <summary>
    /// Type of connector (Database, File, API).
    /// </summary>
    public required string Type { get; set; }

    /// <summary>
    /// Provider implementation (SqlServer, PostgreSQL, MySQL, CSV, Excel, JSON, REST).
    /// </summary>
    public required string Provider { get; set; }

    /// <summary>
    /// Direction capability (source, destination, both).
    /// </summary>
    public required string Direction { get; set; }

    /// <summary>
    /// Whether this connector can be used as a source.
    /// </summary>
    public bool IsSource { get; set; }

    /// <summary>
    /// Whether this connector can be used as a destination.
    /// </summary>
    public bool IsDestination { get; set; }

    /// <summary>
    /// Whether this connector requires credentials.
    /// </summary>
    public bool RequiresCredentials { get; set; }

    /// <summary>
    /// Whether this connector is active.
    /// </summary>
    public bool IsActive { get; set; }
    
    // JSON columns for flexible configuration (PostgreSQL JSONB)
    
    /// <summary>
    /// JSON configuration including connection settings and credentials.
    /// </summary>
    public required string ConfigJson { get; set; }

    /// <summary>
    /// JSON schema defining the data fields.
    /// </summary>
    public string? SchemaJson { get; set; }
    
    // Testing and audit
    
    /// <summary>
    /// When the connector was last tested.
    /// </summary>
    public DateTime? LastTestedAt { get; set; }

    /// <summary>
    /// Result of the last test (Success, Failed).
    /// </summary>
    public string? LastTestResult { get; set; }

    /// <summary>
    /// Message from the last test.
    /// </summary>
    public string? LastTestMessage { get; set; }

    /// <summary>
    /// When the connector was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// When the connector was last updated.
    /// </summary>
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// User who created the connector.
    /// </summary>
    public Guid CreatedBy { get; set; }

    /// <summary>
    /// User who last updated the connector.
    /// </summary>
    public Guid? UpdatedBy { get; set; }

    // Navigation properties
    
    /// <summary>
    /// Tenant that owns this connector.
    /// </summary>
    public Tenant? Tenant { get; set; }
}
