using MultiTenantETL.Domain.Enums;

namespace MultiTenantETL.Application.Tenants.Models;

/// <summary>
/// Result of a tenant switch operation
/// </summary>
public class TenantSwitchResult
{
    public bool Success { get; set; }
    public UserTenantResponse? UserTenant { get; set; }
    public AuthErrorCode? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
}
