using MultiTenantETL.Domain.Entities;
using MultiTenantETL.Domain.Enums;
using MultiTenantETL.Infrastructure.Identity;

namespace MultiTenantETL.Infrastructure.Interfaces;

/// <summary>
/// Service for handling tenant switching operations
/// </summary>
public interface ITenantService
{
    /// <summary>
    /// Switches the user to a different tenant
    /// </summary>
    Task<TenantSwitchResult> SwitchUserTenantAsync(Guid userId, Guid tenantId);
}

/// <summary>
/// Result of a tenant switch operation
/// </summary>
public class TenantSwitchResult
{
    public bool Success { get; set; }
    public UserTenant? UserTenant { get; set; }
    public AuthErrorCode? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
}
