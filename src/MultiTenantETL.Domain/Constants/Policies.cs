namespace MultiTenantETL.Domain.Constants;

/// <summary>
/// Authorization policy names used throughout the application
/// </summary>
public static class Policies
{
    /// <summary>
    /// Policy that validates tenant resource ownership
    /// </summary>
    public const string TenantResource = "TenantResource";

    /// <summary>
    /// Policy requiring SuperAdmin role
    /// </summary>
    public const string RequireSuperAdmin = "RequireSuperAdmin";

    /// <summary>
    /// Policy requiring TenantAdmin, PlatformAdmin, or SuperAdmin role
    /// </summary>
    public const string RequireTenantAdmin = "RequireTenantAdmin";
}
