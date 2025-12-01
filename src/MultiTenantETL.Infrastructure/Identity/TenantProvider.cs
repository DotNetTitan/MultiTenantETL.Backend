using MultiTenantETL.Application.Common.Interfaces;

namespace MultiTenantETL.Infrastructure.Identity;

/// <summary>
/// Scoped tenant context provider for background workers and non-HTTP scenarios.
/// Each service scope gets its own instance, preventing tenant bleed between concurrent jobs.
/// </summary>
public class TenantProvider : ITenantProvider
{
    public Guid? TenantId { get; set; }
    public string? CorrelationId { get; set; }
}
