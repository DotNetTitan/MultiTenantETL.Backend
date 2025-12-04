using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MultiTenantETL.Domain.Entities;

namespace MultiTenantETL.Infrastructure.Persistence.Configurations;

/// <summary>
/// Entity configuration for PipelineExecution.
/// </summary>
public class PipelineExecutionConfiguration : IEntityTypeConfiguration<PipelineExecution>
{
    public void Configure(EntityTypeBuilder<PipelineExecution> builder)
    {
        builder.ToTable("pipeline_executions");

        builder.HasOne(e => e.Pipeline)
            .WithMany()
            .HasForeignKey(e => e.PipelineId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.Tenant)
            .WithMany()
            .HasForeignKey(e => e.TenantId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => e.PipelineId);
        builder.HasIndex(e => e.TenantId);
        builder.HasIndex(e => e.Status);
        builder.HasIndex(e => e.StartTime);

        builder.Property(e => e.SummaryJson)
            .HasColumnType("jsonb");

        builder.Property(e => e.MetadataJson)
            .HasColumnType("jsonb");

        // Store ExecutionStatus enum as string
        builder.Property(e => e.Status)
            .HasConversion<string>();
    }
}
