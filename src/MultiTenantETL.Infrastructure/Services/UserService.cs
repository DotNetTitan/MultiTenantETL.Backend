using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MultiTenantETL.Domain.Enums;
using MultiTenantETL.Infrastructure.Identity;
using MultiTenantETL.Infrastructure.Interfaces;
using MultiTenantETL.Infrastructure.Persistence;

namespace MultiTenantETL.Infrastructure.Services;

public class UserService : IUserService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ApplicationDbContext _context;

    public UserService(
        UserManager<ApplicationUser> userManager,
        ApplicationDbContext context)
    {
        _userManager = userManager;
        _context = context;
    }

    public async Task<ApplicationUser?> GetUserByIdAsync(Guid userId)
    {
        return await _userManager.Users
            .Include(u => u.CurrentTenant)
            .FirstOrDefaultAsync(u => u.Id == userId);
    }

    public async Task<ApplicationUser?> GetUserByEmailAsync(string email)
    {
        return await _userManager.Users
            .Include(u => u.CurrentTenant)
            .FirstOrDefaultAsync(u => u.Email == email);
    }

    public async Task<(List<ApplicationUser> Users, int TotalCount)> GetUsersAsync(
        string? email = null,
        string? name = null,
        bool? isActive = null,
        Guid? tenantId = null,
        int page = 1,
        int pageSize = 20)
    {
        var query = _userManager.Users
            .Include(u => u.CurrentTenant)
            .AsQueryable();

        // Apply filters
        if (!string.IsNullOrWhiteSpace(email))
        {
            query = query.Where(u => u.Email!.Contains(email));
        }

        if (!string.IsNullOrWhiteSpace(name))
        {
            query = query.Where(u =>
                u.FirstName.Contains(name) ||
                u.LastName.Contains(name));
        }

        if (isActive.HasValue)
        {
            query = query.Where(u => u.IsActive == isActive.Value);
        }

        if (tenantId.HasValue)
        {
            query = query.Where(u => u.UserTenants.Any(ut =>
                ut.TenantId == tenantId.Value && ut.IsActive));
        }

        var totalCount = await query.CountAsync();

        // Apply pagination
        var users = await query
            .OrderBy(u => u.Email)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (users, totalCount);
    }

    public async Task<ServiceResult<ApplicationUser>> UpdateUserAsync(
        Guid userId,
        string firstName,
        string lastName,
        string email)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            return ServiceResult<ApplicationUser>.FailureResult(
                AuthErrorCode.UserNotFound,
                "User not found");
        }

        // Check if email is already taken by another user
        if (user.Email != email)
        {
            var existingUser = await _userManager.FindByEmailAsync(email);
            if (existingUser != null && existingUser.Id != userId)
            {
                return ServiceResult<ApplicationUser>.FailureResult(
                    AuthErrorCode.EmailAlreadyExists,
                    "Email is already in use");
            }
        }

        user.FirstName = firstName;
        user.LastName = lastName;
        user.Email = email;
        user.UserName = email;

        var result = await _userManager.UpdateAsync(user);

        if (!result.Succeeded)
        {
            return ServiceResult<ApplicationUser>.FailureResult(
                AuthErrorCode.ValidationError,
                string.Join(", ", result.Errors.Select(e => e.Description)));
        }

        return ServiceResult<ApplicationUser>.SuccessResult(user);
    }

    public async Task<ServiceResult<ApplicationUser>> UpdateUserStatusAsync(Guid userId, bool isActive)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            return ServiceResult<ApplicationUser>.FailureResult(
                AuthErrorCode.UserNotFound,
                "User not found");
        }

        user.IsActive = isActive;
        var result = await _userManager.UpdateAsync(user);

        if (!result.Succeeded)
        {
            return ServiceResult<ApplicationUser>.FailureResult(
                AuthErrorCode.ValidationError,
                string.Join(", ", result.Errors.Select(e => e.Description)));
        }

        return ServiceResult<ApplicationUser>.SuccessResult(user);
    }

    public async Task<ServiceResult> DeleteUserAsync(Guid userId)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            return ServiceResult.FailureResult(
                AuthErrorCode.UserNotFound,
                "User not found");
        }

        // Soft delete by deactivating
        user.IsActive = false;
        var result = await _userManager.UpdateAsync(user);

        if (!result.Succeeded)
        {
            return ServiceResult.FailureResult(
                AuthErrorCode.ValidationError,
                string.Join(", ", result.Errors.Select(e => e.Description)));
        }

        return ServiceResult.SuccessResult();
    }

    public async Task<ServiceResult> AssignRoleAsync(Guid userId, string roleName)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            return ServiceResult.FailureResult(
                AuthErrorCode.UserNotFound,
                "User not found");
        }

        var result = await _userManager.AddToRoleAsync(user, roleName);

        if (!result.Succeeded)
        {
            return ServiceResult.FailureResult(
                AuthErrorCode.ValidationError,
                string.Join(", ", result.Errors.Select(e => e.Description)));
        }

        return ServiceResult.SuccessResult();
    }

    public async Task<ServiceResult> RemoveRoleAsync(Guid userId, string roleName)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            return ServiceResult.FailureResult(
                AuthErrorCode.UserNotFound,
                "User not found");
        }

        var result = await _userManager.RemoveFromRoleAsync(user, roleName);

        if (!result.Succeeded)
        {
            return ServiceResult.FailureResult(
                AuthErrorCode.ValidationError,
                string.Join(", ", result.Errors.Select(e => e.Description)));
        }

        return ServiceResult.SuccessResult();
    }

    public async Task<List<string>> GetUserRolesAsync(Guid userId)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            return new List<string>();
        }

        var roles = await _userManager.GetRolesAsync(user);
        return roles.ToList();
    }

    public async Task<ServiceResult> AdminResetPasswordAsync(Guid userId, string newPassword)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            return ServiceResult.FailureResult(
                AuthErrorCode.UserNotFound,
                "User not found");
        }

        // Remove existing password
        await _userManager.RemovePasswordAsync(user);

        // Add new password
        var result = await _userManager.AddPasswordAsync(user, newPassword);

        if (!result.Succeeded)
        {
            return ServiceResult.FailureResult(
                AuthErrorCode.ValidationError,
                string.Join(", ", result.Errors.Select(e => e.Description)));
        }

        return ServiceResult.SuccessResult();
    }

    public async Task<List<UserTenant>> GetUserTenantsAsync(Guid userId)
    {
        return await _context.UserTenants
            .Include(ut => ut.Tenant)
            .Where(ut => ut.UserId == userId && ut.IsActive)
            .OrderBy(ut => ut.Tenant.Name)
            .ToListAsync();
    }
}
