namespace MultiTenantETL.Application.Common.Interfaces;

/// <summary>
/// Provides tenant context for both HTTP and non-HTTP scenarios (e.g., background workers).
/// Scoped per request/job to ensure tenant isolation.
/// </summary>
public interface ITenantProvider
{
    /// <summary>
    /// Gets or sets the current tenant ID for this scope
    /// </summary>
    Guid? TenantId { get; set; }

    /// <summary>
    /// Gets or sets the correlation ID for tracing across operations
    /// </summary>
    string? CorrelationId { get; set; }
}
