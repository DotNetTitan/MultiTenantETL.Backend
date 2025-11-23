using MultiTenantETL.Infrastructure.Identity;

namespace MultiTenantETL.Infrastructure.Interfaces;

/// <summary>
/// Service for managing users
/// </summary>
public interface IUserService
{
    /// <summary>
    /// Gets a user by ID
    /// </summary>
    Task<ApplicationUser?> GetUserByIdAsync(Guid userId);

    /// <summary>
    /// Gets a user by email
    /// </summary>
    Task<ApplicationUser?> GetUserByEmailAsync(string email);

    /// <summary>
    /// Gets all users with optional filtering and pagination
    /// </summary>
    Task<(List<ApplicationUser> Users, int TotalCount)> GetUsersAsync(
        string? email = null,
        string? name = null,
        bool? isActive = null,
        Guid? tenantId = null,
        int page = 1,
        int pageSize = 20);

    /// <summary>
    /// Updates a user's profile information
    /// </summary>
    Task<ServiceResult<ApplicationUser>> UpdateUserAsync(
        Guid userId,
        string firstName,
        string lastName,
        string email);

    /// <summary>
    /// Updates a user's active status
    /// </summary>
    Task<ServiceResult<ApplicationUser>> UpdateUserStatusAsync(Guid userId, bool isActive);

    /// <summary>
    /// Deletes a user (soft delete)
    /// </summary>
    Task<ServiceResult> DeleteUserAsync(Guid userId);

    /// <summary>
    /// Assigns a role to a user
    /// </summary>
    Task<ServiceResult> AssignRoleAsync(Guid userId, string roleName);

    /// <summary>
    /// Removes a role from a user
    /// </summary>
    Task<ServiceResult> RemoveRoleAsync(Guid userId, string roleName);

    /// <summary>
    /// Gets all roles assigned to a user
    /// </summary>
    Task<List<string>> GetUserRolesAsync(Guid userId);

    /// <summary>
    /// Resets a user's password (admin operation)
    /// </summary>
    Task<ServiceResult> AdminResetPasswordAsync(Guid userId, string newPassword);

    /// <summary>
    /// Gets user's tenant memberships
    /// </summary>
    Task<List<UserTenant>> GetUserTenantsAsync(Guid userId);
}
