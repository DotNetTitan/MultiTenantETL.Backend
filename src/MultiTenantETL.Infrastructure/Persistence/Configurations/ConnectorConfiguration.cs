using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MultiTenantETL.Domain.Entities;

namespace MultiTenantETL.Infrastructure.Persistence.Configurations;

/// <summary>
/// Entity configuration for Connector.
/// </summary>
public class ConnectorConfiguration : IEntityTypeConfiguration<Connector>
{
    public void Configure(EntityTypeBuilder<Connector> builder)
    {
        builder.ToTable("connectors");

        builder.HasOne(c => c.Tenant)
            .WithMany()
            .HasForeignKey(c => c.TenantId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(c => c.TenantId);
        builder.HasIndex(c => c.Type);
        builder.HasIndex(c => c.Provider);
        builder.HasIndex(c => new { c.TenantId, c.Name });

        // Configure JSON columns for PostgreSQL JSONB
        builder.Property(c => c.ConfigJson)
            .HasColumnType("jsonb");

        builder.Property(c => c.SchemaJson)
            .HasColumnType("jsonb");
    }
}
