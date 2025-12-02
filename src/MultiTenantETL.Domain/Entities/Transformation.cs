using MultiTenantETL.Domain.Interfaces;

namespace MultiTenantETL.Domain.Entities;

public class Transformation : ITenantResource
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    
    // Transformation type: Filter, Map, Trim, CaseConvert, Substring, Replace, Script
    public required string Type { get; set; }
    
    // JSON column for flexible configuration (PostgreSQL JSONB)
    // Stores type-specific configuration (filter rules, mappings, script code, etc.)
    public required string ConfigJson { get; set; }
    
    // Audit fields
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid CreatedBy { get; set; }
    public Guid? UpdatedBy { get; set; }

    // Navigation properties
    public Tenant? Tenant { get; set; }
}
