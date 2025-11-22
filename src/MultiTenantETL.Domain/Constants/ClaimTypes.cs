namespace MultiTenantETL.Domain.Constants;

/// <summary>
/// JWT claim type constants used throughout the application
/// </summary>
public static class ClaimTypes
{
    /// <summary>
    /// Custom claim for tenant ID
    /// </summary>
    public const string TenantId = "tenant_id";

    /// <summary>
    /// Custom claim for tenant name
    /// </summary>
    public const string TenantName = "tenant_name";

    /// <summary>
    /// Individual permission claim (array)
    /// </summary>
    public const string Permission = "permission";

    /// <summary>
    /// JSON array of all permissions
    /// </summary>
    public const string Permissions = "permissions";
}
