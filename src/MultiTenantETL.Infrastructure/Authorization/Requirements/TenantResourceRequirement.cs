using Microsoft.AspNetCore.Authorization;

namespace MultiTenantETL.Infrastructure.Authorization.Requirements;

/// <summary>
/// Authorization requirement for tenant resource isolation.
/// Ensures users can only access resources within their current tenant.
/// </summary>
public class TenantResourceRequirement : IAuthorizationRequirement
{
    // No additional properties needed - just validates tenant ownership
}
