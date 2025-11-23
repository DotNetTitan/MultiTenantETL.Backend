using Microsoft.AspNetCore.Authorization;
using MultiTenantETL.Application.Common.Interfaces;
using MultiTenantETL.Domain.Interfaces;
using MultiTenantETL.Infrastructure.Authorization.Requirements;

namespace MultiTenantETL.Infrastructure.Authorization.Handlers;

/// <summary>
/// Authorization handler that enforces tenant-level resource isolation.
/// Ensures users can only access resources belonging to their current tenant.
/// </summary>
public class TenantResourceAuthorizationHandler : AuthorizationHandler<TenantResourceRequirement, ITenantResource>
{
    private readonly ICurrentUserService _currentUser;

    public TenantResourceAuthorizationHandler(ICurrentUserService currentUser)
    {
        _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
    }

    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        TenantResourceRequirement requirement,
        ITenantResource resource)
    {
        if (resource == null)
        {
            // If resource is null, we can't validate - fail the requirement
            return Task.CompletedTask;
        }

        // Get the current user's tenant ID
        var userTenantId = _currentUser.GetTenantId();

        // User can only access resources in their current tenant
        if (resource.TenantId == userTenantId)
        {
            context.Succeed(requirement);
        }

        // If tenant IDs don't match, requirement fails automatically
        return Task.CompletedTask;
    }
}
