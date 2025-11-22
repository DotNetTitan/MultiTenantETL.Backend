namespace MultiTenantETL.Domain.Constants;

/// <summary>
/// Role name constants used throughout the application
/// </summary>
public static class Roles
{
    /// <summary>
    /// System administrator with full access to all tenants and system configuration
    /// </summary>
    public const string SuperAdmin = "SuperAdmin";

    /// <summary>
    /// Tenant administrator with full access within their tenant
    /// </summary>
    public const string TenantAdmin = "TenantAdmin";

    /// <summary>
    /// Standard user with read and basic write access
    /// </summary>
    public const string User = "User";

    /// <summary>
    /// Read-only access to tenant data
    /// </summary>
    public const string Viewer = "Viewer";
}
