using MultiTenantETL.Domain.Interfaces;

namespace MultiTenantETL.Domain.Entities;

public class Connector : ITenantResource
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    
    // Relational columns for querying/filtering
    public required string Type { get; set; } // Database, File, API
    public required string Provider { get; set; } // SqlServer, PostgreSQL, MySQL, CSV, Excel, JSON, REST
    public required string Direction { get; set; } // source, destination, both
    public bool IsSource { get; set; }
    public bool IsDestination { get; set; }
    public bool RequiresCredentials { get; set; }
    public bool IsActive { get; set; }
    
    // JSON columns for flexible configuration (PostgreSQL JSONB)
    public required string ConfigJson { get; set; } // Connection settings, credentials, type-specific config
    public string? SchemaJson { get; set; } // Field definitions, version, metadata
    
    // Testing and audit
    public DateTime? LastTestedAt { get; set; }
    public string? LastTestResult { get; set; } // Success, Failed
    public string? LastTestMessage { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid CreatedBy { get; set; }
    public Guid? UpdatedBy { get; set; }

    // Navigation properties
    public Tenant? Tenant { get; set; }
}
