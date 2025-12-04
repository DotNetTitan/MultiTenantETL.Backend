using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MultiTenantETL.Domain.Entities;

namespace MultiTenantETL.Infrastructure.Persistence.Configurations;

/// <summary>
/// Entity configuration for Transformation.
/// </summary>
public class TransformationConfiguration : IEntityTypeConfiguration<Transformation>
{
    public void Configure(EntityTypeBuilder<Transformation> builder)
    {
        builder.ToTable("transformations");

        builder.HasOne(t => t.Tenant)
            .WithMany()
            .HasForeignKey(t => t.TenantId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(t => t.TenantId);
        builder.HasIndex(t => t.Type);
        builder.HasIndex(t => new { t.TenantId, t.Name });

        builder.Property(t => t.ConfigJson)
            .HasColumnType("jsonb");
    }
}
