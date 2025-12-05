using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MultiTenantETL.Domain.Entities;

namespace MultiTenantETL.Infrastructure.Persistence.Configurations;

/// <summary>
/// Entity configuration for Tenant.
/// </summary>
public class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.ToTable("tenants");

        // Slug should be unique for URL-friendly tenant identification
        builder.HasIndex(t => t.Slug)
            .IsUnique();

        // Index for filtering by active status
        builder.HasIndex(t => t.IsActive);

        // Composite index for common query patterns
        builder.HasIndex(t => new { t.IsActive, t.Name });
    }
}
