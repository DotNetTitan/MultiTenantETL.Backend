using System.Collections.Immutable;
using System.Security.Claims;
using MultiTenantETL.Infrastructure.Identity;

namespace MultiTenantETL.Infrastructure.Interfaces;

/// <summary>
/// Service for building JWT claims principals
/// </summary>
public interface IClaimsService
{
    /// <summary>
    /// Builds a ClaimsPrincipal with all necessary claims for the user
    /// </summary>
    Task<ClaimsPrincipal> BuildClaimsPrincipalAsync(
        ApplicationUser user, 
        ImmutableArray<string> scopes);
}
