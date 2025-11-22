namespace MultiTenantETL.Application.Common.Interfaces;

/// <summary>
/// Service to easily access current authenticated user's claims and permissions
/// </summary>
public interface ICurrentUserService
{
    /// <summary>
    /// Gets the current user's ID
    /// </summary>
    Guid GetUserId();

    /// <summary>
    /// Gets the current user's active tenant ID
    /// </summary>
    Guid GetTenantId();

    /// <summary>
    /// Gets the current user's role for the active tenant
    /// </summary>
    string GetRole();

    /// <summary>
    /// Gets the current user's email
    /// </summary>
    string GetEmail();

    /// <summary>
    /// Gets the current user's full name
    /// </summary>
    string GetName();

    /// <summary>
    /// Gets the current user's tenant name
    /// </summary>
    string GetTenantName();

    /// <summary>
    /// Gets all permissions for the current user
    /// </summary>
    List<string> GetPermissions();

    /// <summary>
    /// Checks if the current user has the specified permission.
    /// Supports wildcard permissions (e.g., "pipelines:*", "*:read", "*:*")
    /// </summary>
    bool HasPermission(string permission);

    /// <summary>
    /// Checks if the current user is in the specified role
    /// </summary>
    bool IsInRole(string role);
}
