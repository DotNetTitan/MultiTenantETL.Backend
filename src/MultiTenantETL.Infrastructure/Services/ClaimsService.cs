using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MultiTenantETL.Infrastructure.Identity;
using MultiTenantETL.Infrastructure.Interfaces;
using MultiTenantETL.Infrastructure.Persistence;
using OpenIddict.Abstractions;
using System.Collections.Immutable;
using System.Security.Claims;
using CustomClaims = MultiTenantETL.Domain.Constants.ClaimTypes;

namespace MultiTenantETL.Infrastructure.Services;

public class ClaimsService : IClaimsService
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly ApplicationDbContext _context;

    public ClaimsService(
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager,
        ApplicationDbContext context)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _roleManager = roleManager;
        _context = context;
    }

    public async Task<ClaimsPrincipal> BuildClaimsPrincipalAsync(
        ApplicationUser user,
        ImmutableArray<string> scopes)
    {
        var principal = await _signInManager.CreateUserPrincipalAsync(user);
        var identity = (ClaimsIdentity)principal.Identity!;

        // Add base claims
        AddBaseClaims(identity, user);

        // Add tenant-specific claims
        await AddTenantClaimsAsync(identity, user);

        // Set claim destinations
        SetClaimDestinations(identity);

        return principal;
    }

    private void AddBaseClaims(ClaimsIdentity identity, ApplicationUser user)
    {
        identity.SetClaim(OpenIddictConstants.Claims.Subject, user.Id.ToString());
        identity.SetClaim(OpenIddictConstants.Claims.Email, user.Email);
        identity.SetClaim(OpenIddictConstants.Claims.Name, $"{user.FirstName} {user.LastName}");
        identity.SetClaim(CustomClaims.TenantId, user.CurrentTenantId?.ToString() ?? "");
    }

    private async Task AddTenantClaimsAsync(ClaimsIdentity identity, ApplicationUser user)
    {
        // Always add global roles first (e.g., SuperAdmin)
        var globalRoles = await _userManager.GetRolesAsync(user);
        foreach (var role in globalRoles)
        {
            identity.AddClaim(new Claim(ClaimTypes.Role, role));
            identity.AddClaim(new Claim(OpenIddictConstants.Claims.Role, role));
        }

        // If user has a current tenant, add tenant-specific role and permissions
        if (user.CurrentTenantId.HasValue)
        {
            var userTenant = await _context.UserTenants
                .IgnoreQueryFilters()
                .Include(ut => ut.Tenant)
                .FirstOrDefaultAsync(ut =>
                    ut.UserId == user.Id &&
                    ut.TenantId == user.CurrentTenantId.Value &&
                    ut.IsActive);

            if (userTenant != null)
            {
                // Add tenant-specific role (if different from global role)
                if (!globalRoles.Contains(userTenant.RoleCode))
                {
                    identity.AddClaim(new Claim(ClaimTypes.Role, userTenant.RoleCode));
                    identity.AddClaim(new Claim(OpenIddictConstants.Claims.Role, userTenant.RoleCode));
                }

                // Add tenant name claim
                identity.SetClaim(CustomClaims.TenantName, userTenant.Tenant!.Name);

                // Add permission claims based on the highest role
                var roleForPermissions = globalRoles.Contains(Domain.Constants.Roles.SuperAdmin)
                    ? Domain.Constants.Roles.SuperAdmin
                    : globalRoles.Contains(Domain.Constants.Roles.PlatformAdmin)
                        ? Domain.Constants.Roles.PlatformAdmin
                    : userTenant.RoleCode;
                await AddPermissionClaimsAsync(identity, roleForPermissions);
            }
            else if (globalRoles.Contains(Domain.Constants.Roles.SuperAdmin) || globalRoles.Contains(Domain.Constants.Roles.PlatformAdmin))
            {
                // SuperAdmin/PlatformAdmin can access any tenant, even without UserTenant record
                // Add tenant name if tenant exists
                var tenant = await _context.Tenants
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(t => t.Id == user.CurrentTenantId.Value && t.Status == Domain.Enums.TenantStatus.Active);
                if (tenant != null)
                {
                    identity.SetClaim(CustomClaims.TenantName, tenant.Name);
                }

                // Add highest global role permissions
                await AddPermissionClaimsAsync(identity,
                    globalRoles.Contains(Domain.Constants.Roles.SuperAdmin)
                        ? Domain.Constants.Roles.SuperAdmin
                        : Domain.Constants.Roles.PlatformAdmin);
            }
        }
        else if (globalRoles.Any())
        {
            // No tenant selected, use global role for permissions
            await AddPermissionClaimsAsync(identity, globalRoles.First());
        }
    }

    private async Task AddPermissionClaimsAsync(ClaimsIdentity identity, string roleCode)
    {
        var role = await _roleManager.FindByNameAsync(roleCode);
        if (role?.Permissions == null || !role.Permissions.Any())
            return;

        // Add individual permission claims
        foreach (var permission in role.Permissions)
        {
            identity.AddClaim(new Claim(CustomClaims.Permission, permission));
        }

        // Add permissions as a single JSON claim for easy access
        identity.SetClaim(CustomClaims.Permissions,
            System.Text.Json.JsonSerializer.Serialize(role.Permissions));
    }

    private void SetClaimDestinations(ClaimsIdentity identity)
    {
        identity.SetDestinations(claim => claim.Type switch
        {
            "AspNet.Identity.SecurityStamp" => ImmutableArray<string>.Empty,

            OpenIddictConstants.Claims.Subject
            or OpenIddictConstants.Claims.Name
            or OpenIddictConstants.Claims.Email
            or OpenIddictConstants.Claims.Role
            or ClaimTypes.Role
                => new[] { OpenIddictConstants.Destinations.AccessToken, OpenIddictConstants.Destinations.IdentityToken },

            // Tenant claims go to both tokens so frontend can read them from id_token
            var type when type == CustomClaims.TenantId
                       || type == CustomClaims.TenantName
                => new[] { OpenIddictConstants.Destinations.AccessToken, OpenIddictConstants.Destinations.IdentityToken },

            // Permission claims go to access token only (not needed in id_token)
            var type when type == CustomClaims.Permission
                       || type == CustomClaims.Permissions
                => new[] { OpenIddictConstants.Destinations.AccessToken },

            _ => new[] { OpenIddictConstants.Destinations.AccessToken }
        });
    }
}
