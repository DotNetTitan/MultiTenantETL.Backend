using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MultiTenantETL.Domain.Entities;

namespace MultiTenantETL.Infrastructure.Persistence.Configurations;

/// <summary>
/// Entity configuration for ExecutionBatch.
/// </summary>
public class ExecutionBatchConfiguration : IEntityTypeConfiguration<ExecutionBatch>
{
    public void Configure(EntityTypeBuilder<ExecutionBatch> builder)
    {
        builder.ToTable("execution_batches");

        builder.HasOne(b => b.Execution)
            .WithMany()
            .HasForeignKey(b => b.ExecutionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(b => b.Tenant)
            .WithMany()
            .HasForeignKey(b => b.TenantId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(b => b.ExecutionId);
        builder.HasIndex(b => b.TenantId);
        
        builder.HasIndex(b => new { b.ExecutionId, b.BatchIndex })
            .IsUnique();

        builder.Property(b => b.CheckpointInfoJson)
            .HasColumnType("jsonb");

        // Store BatchStatus enum as string
        builder.Property(b => b.Status)
            .HasConversion<string>();
    }
}
