using Microsoft.AspNetCore.Authorization;

namespace MultiTenantETL.Infrastructure.Authorization.Requirements;

/// <summary>
/// Authorization requirement for permission-based access control.
/// Checks if the current user has the specified permission in their role.
/// </summary>
public class PermissionRequirement : IAuthorizationRequirement
{
    /// <summary>
    /// The permission required to access the resource (e.g., "pipelines:create", "users:delete")
    /// </summary>
    public string Permission { get; }

    public PermissionRequirement(string permission)
    {
        if (string.IsNullOrWhiteSpace(permission))
            throw new ArgumentException("Permission cannot be null or empty", nameof(permission));

        Permission = permission;
    }
}
