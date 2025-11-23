using Microsoft.AspNetCore.Authorization;
using MultiTenantETL.Application.Common.Interfaces;
using MultiTenantETL.Infrastructure.Authorization.Requirements;

namespace MultiTenantETL.Infrastructure.Authorization.Handlers;

/// <summary>
/// Authorization handler that validates permission-based access control.
/// Supports exact permissions, wildcard permissions, and super admin access.
/// </summary>
public class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    private readonly ICurrentUserService _currentUser;

    public PermissionAuthorizationHandler(ICurrentUserService currentUser)
    {
        _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
    }

    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        // Get user's permissions
        var userPermissions = _currentUser.GetPermissions();

        // Check for exact permission match
        if (userPermissions.Contains(requirement.Permission))
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        // Parse permission for wildcard checking
        var parts = requirement.Permission.Split(':');
        if (parts.Length == 2)
        {
            var resource = parts[0];
            var action = parts[1];

            // Check {resource}:* (e.g., "pipelines:*" grants all pipeline permissions)
            if (userPermissions.Contains($"{resource}:*"))
            {
                context.Succeed(requirement);
                return Task.CompletedTask;
            }

            // Check *:{action} (e.g., "*:read" grants read on all resources)
            if (userPermissions.Contains($"*:{action}"))
            {
                context.Succeed(requirement);
                return Task.CompletedTask;
            }
        }

        // Check for super admin permission (*:*)
        if (userPermissions.Contains("*:*"))
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        // Permission not found - requirement fails (no need to call Fail explicitly)
        return Task.CompletedTask;
    }
}
