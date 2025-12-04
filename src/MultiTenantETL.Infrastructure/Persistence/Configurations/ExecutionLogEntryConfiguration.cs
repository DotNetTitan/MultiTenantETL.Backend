using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MultiTenantETL.Domain.Entities;

namespace MultiTenantETL.Infrastructure.Persistence.Configurations;

/// <summary>
/// Entity configuration for ExecutionLogEntry.
/// </summary>
public class ExecutionLogEntryConfiguration : IEntityTypeConfiguration<ExecutionLogEntry>
{
    public void Configure(EntityTypeBuilder<ExecutionLogEntry> builder)
    {
        builder.ToTable("execution_logs");

        builder.HasOne(l => l.Execution)
            .WithMany()
            .HasForeignKey(l => l.ExecutionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(l => l.Tenant)
            .WithMany()
            .HasForeignKey(l => l.TenantId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(l => l.ExecutionId);
        builder.HasIndex(l => l.TenantId);
        builder.HasIndex(l => l.Timestamp);
        builder.HasIndex(l => l.Level);
    }
}
