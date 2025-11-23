using MultiTenantETL.Domain.Entities;
using MultiTenantETL.Domain.Enums;
using MultiTenantETL.Infrastructure.Identity;

namespace MultiTenantETL.Infrastructure.Interfaces;

/// <summary>
/// Service for handling tenant operations
/// </summary>
public interface ITenantService
{
    /// <summary>
    /// Creates a new tenant
    /// </summary>
    Task<ServiceResult<Tenant>> CreateTenantAsync(string name, string slug);

    /// <summary>
    /// Gets a tenant by ID
    /// </summary>
    Task<Tenant?> GetTenantByIdAsync(Guid tenantId);

    /// <summary>
    /// Gets a tenant by slug
    /// </summary>
    Task<Tenant?> GetTenantBySlugAsync(string slug);

    /// <summary>
    /// Gets all tenants (admin only)
    /// </summary>
    Task<List<Tenant>> GetAllTenantsAsync();

    /// <summary>
    /// Gets all tenants for a specific user
    /// </summary>
    Task<List<UserTenant>> GetUserTenantsAsync(Guid userId);

    /// <summary>
    /// Updates a tenant
    /// </summary>
    Task<ServiceResult<Tenant>> UpdateTenantAsync(Guid tenantId, string name, bool? isActive);

    /// <summary>
    /// Deletes a tenant (soft delete by setting IsActive = false)
    /// </summary>
    Task<ServiceResult> DeleteTenantAsync(Guid tenantId);

    /// <summary>
    /// Adds a user to a tenant with a specific role
    /// </summary>
    Task<ServiceResult<UserTenant>> AddUserToTenantAsync(Guid userId, Guid tenantId, string roleCode);

    /// <summary>
    /// Removes a user from a tenant
    /// </summary>
    Task<ServiceResult> RemoveUserFromTenantAsync(Guid userId, Guid tenantId);

    /// <summary>
    /// Updates a user's role within a tenant
    /// </summary>
    Task<ServiceResult<UserTenant>> UpdateUserTenantRoleAsync(Guid userId, Guid tenantId, string roleCode);

    /// <summary>
    /// Gets all users in a tenant
    /// </summary>
    Task<List<UserTenant>> GetTenantUsersAsync(Guid tenantId);

    /// <summary>
    /// Switches the user to a different tenant
    /// </summary>
    Task<TenantSwitchResult> SwitchUserTenantAsync(Guid userId, Guid tenantId);
}

/// <summary>
/// Generic service result
/// </summary>
public class ServiceResult
{
    public bool Success { get; set; }
    public AuthErrorCode? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }

    public static ServiceResult SuccessResult() => new() { Success = true };
    public static ServiceResult FailureResult(AuthErrorCode errorCode, string errorMessage) =>
        new() { Success = false, ErrorCode = errorCode, ErrorMessage = errorMessage };
}

/// <summary>
/// Generic service result with data
/// </summary>
public class ServiceResult<T> : ServiceResult
{
    public T? Data { get; set; }

    public static ServiceResult<T> SuccessResult(T data) => new() { Success = true, Data = data };
    public static new ServiceResult<T> FailureResult(AuthErrorCode errorCode, string errorMessage) =>
        new() { Success = false, ErrorCode = errorCode, ErrorMessage = errorMessage };
}

/// <summary>
/// Result of a tenant switch operation
/// </summary>
public class TenantSwitchResult
{
    public bool Success { get; set; }
    public UserTenant? UserTenant { get; set; }
    public AuthErrorCode? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
}
