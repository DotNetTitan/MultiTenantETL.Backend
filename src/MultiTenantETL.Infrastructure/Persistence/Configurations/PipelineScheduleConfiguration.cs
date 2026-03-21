using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MultiTenantETL.Domain.Entities;

namespace MultiTenantETL.Infrastructure.Persistence.Configurations;

/// <summary>
/// Entity configuration for PipelineSchedule.
/// </summary>
public class PipelineScheduleConfiguration : IEntityTypeConfiguration<PipelineSchedule>
{
    public void Configure(EntityTypeBuilder<PipelineSchedule> builder)
    {
        builder.ToTable("pipeline_schedules");

        builder.HasKey(s => s.Id);

        builder.HasOne(s => s.Pipeline)
            .WithOne(p => p.Schedule)
            .HasForeignKey<PipelineSchedule>(s => s.PipelineId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(s => s.Tenant)
            .WithMany()
            .HasForeignKey(s => s.TenantId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes for efficient querying
        builder.HasIndex(s => s.TenantId);
        builder.HasIndex(s => s.PipelineId).IsUnique(); // One schedule per pipeline
        builder.HasIndex(s => s.IsActive);
        builder.HasIndex(s => s.NextRunAt)
            .HasFilter("\"IsActive\" = true"); // Partial index for active schedules

        // Composite index for scheduler polling
        builder.HasIndex(s => new { s.IsActive, s.NextRunAt });

        // Property configurations
        builder.Property(s => s.CronExpression)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(s => s.Timezone)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(s => s.Description)
            .HasMaxLength(500);

        builder.Property(s => s.QuartzJobKey)
            .HasMaxLength(200);

        builder.Property(s => s.QuartzTriggerKey)
            .HasMaxLength(200);

        builder.Property(s => s.MaxConsecutiveFailures)
            .HasDefaultValue(5);

        builder.Property(s => s.ConsecutiveFailures)
            .HasDefaultValue(0);

        builder.Property(s => s.IsPausedByPipeline)
            .HasDefaultValue(false);
    }
}
