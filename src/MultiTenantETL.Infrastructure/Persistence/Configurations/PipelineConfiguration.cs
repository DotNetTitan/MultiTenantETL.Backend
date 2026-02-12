using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MultiTenantETL.Domain.Entities;

namespace MultiTenantETL.Infrastructure.Persistence.Configurations;

/// <summary>
/// Entity configuration for Pipeline.
/// </summary>
public class PipelineConfiguration : IEntityTypeConfiguration<Pipeline>
{
    public void Configure(EntityTypeBuilder<Pipeline> builder)
    {
        builder.ToTable("pipelines");

        builder.HasOne(p => p.Tenant)
            .WithMany()
            .HasForeignKey(p => p.TenantId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(p => p.SourceConnector)
            .WithMany()
            .HasForeignKey(p => p.SourceConnectorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(p => p.DestinationConnector)
            .WithMany()
            .HasForeignKey(p => p.DestinationConnectorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(p => p.TenantId);
        builder.HasIndex(p => p.Status);
        builder.HasIndex(p => new { p.TenantId, p.Name });

        builder.Property(p => p.FieldMappingsJson)
            .HasColumnType("jsonb");

        builder.Property(p => p.EmailNotificationsEnabled)
            .HasDefaultValue(true);
    }
}
