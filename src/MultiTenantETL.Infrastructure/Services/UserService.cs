using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MultiTenantETL.Application.Common.Interfaces;
using MultiTenantETL.Application.Common.Models;
using MultiTenantETL.Application.Interfaces;
using MultiTenantETL.Domain.Enums;
using MultiTenantETL.Infrastructure.Identity;
using MultiTenantETL.Infrastructure.Interfaces;
using MultiTenantETL.Infrastructure.Persistence;
using OpenIddict.Abstractions;

namespace MultiTenantETL.Infrastructure.Services;

public class UserService : IUserService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ApplicationDbContext _context;
    private readonly IOpenIddictTokenManager _tokenManager;
    private readonly ICurrentUserService _currentUserService;
    private readonly ITenantService _tenantService;
    private readonly IEmailService _emailService;

    public UserService(
        UserManager<ApplicationUser> userManager,
        ApplicationDbContext context,
        IOpenIddictTokenManager tokenManager,
        ICurrentUserService currentUserService,
        ITenantService tenantService,
        IEmailService emailService)
    {
        _userManager = userManager;
        _context = context;
        _tokenManager = tokenManager;
        _currentUserService = currentUserService;
        _tenantService = tenantService;
        _emailService = emailService;
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
        UserStatus? status = null,
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
            var emailLower = email.ToLower();
            query = query.Where(u => u.Email!.ToLower().Contains(emailLower));
        }

        if (!string.IsNullOrWhiteSpace(name))
        {
            var nameLower = name.ToLower();
            query = query.Where(u =>
                u.FirstName.ToLower().Contains(nameLower) ||
                u.LastName.ToLower().Contains(nameLower));
        }

        if (status.HasValue)
        {
            query = query.Where(u => u.Status == status.Value);
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

    public async Task<ServiceResult<ApplicationUser>> UpdateUserStatusAsync(Guid userId, UserStatus status)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            return ServiceResult<ApplicationUser>.FailureResult(
                AuthErrorCode.UserNotFound,
                "User not found");
        }

        user.Status = status;
        var result = await _userManager.UpdateAsync(user);

        if (!result.Succeeded)
        {
            return ServiceResult<ApplicationUser>.FailureResult(
                AuthErrorCode.ValidationError,
                string.Join(", ", result.Errors.Select(e => e.Description)));
        }

        if (status != UserStatus.Active)
        {
            await _userManager.UpdateSecurityStampAsync(user);
            await RevokeUserTokensAsync(userId);
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

        // 1. Scramble Email and Username to free them up
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        user.Email = $"{user.Email}_deleted_{timestamp}";
        user.UserName = $"{user.UserName}_deleted_{timestamp}";
        user.NormalizedEmail = user.Email.ToUpperInvariant();
        user.NormalizedUserName = user.UserName.ToUpperInvariant();

        // 2. Set Status and metadata
        user.Status = UserStatus.Deleted;
        user.DeletedAt = DateTime.UtcNow;
        user.DeletedBy = _currentUserService.GetUserId();

        await _userManager.UpdateSecurityStampAsync(user);
        var result = await _userManager.UpdateAsync(user);

        if (!result.Succeeded)
        {
            return ServiceResult.FailureResult(
                AuthErrorCode.ValidationError,
                string.Join(", ", result.Errors.Select(e => e.Description)));
        }

        // 3. Remove all tenant memberships
        var userTenants = await _context.UserTenants
            .Where(ut => ut.UserId == userId)
            .ToListAsync();
        _context.UserTenants.RemoveRange(userTenants);

        // 4. Soft-delete personal workspace tenant
        var personalTenantSlug = $"user-{userId.ToString()[..8]}";
        var personalTenant = await _context.Tenants
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Slug == personalTenantSlug);

        if (personalTenant != null)
        {
            await _tenantService.DeleteTenantAsync(personalTenant.Id);
        }

        await _context.SaveChangesAsync();
        await RevokeUserTokensAsync(userId);

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

        var oldRoles = await _userManager.GetRolesAsync(user);
        var oldRole = oldRoles.FirstOrDefault() ?? "None";

        var result = await _userManager.AddToRoleAsync(user, roleName);

        if (!result.Succeeded)
        {
            return ServiceResult.FailureResult(
                AuthErrorCode.ValidationError,
                string.Join(", ", result.Errors.Select(e => e.Description)));
        }

        var currentUser = await _userManager.FindByIdAsync(_currentUserService.GetUserId().ToString());
        var changedBy = currentUser?.Email ?? "System";

        _ = _emailService.SendRoleChangedNotificationAsync(
            user.Email ?? string.Empty,
            user.FirstName ?? "User",
            oldRole,
            roleName,
            changedBy);

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

        var resetToken = await _userManager.GeneratePasswordResetTokenAsync(user);
        var result = await _userManager.ResetPasswordAsync(user, resetToken, newPassword);

        if (!result.Succeeded)
        {
            return ServiceResult.FailureResult(
                AuthErrorCode.ValidationError,
                string.Join(", ", result.Errors.Select(e => e.Description)));
        }

        await RevokeUserTokensAsync(userId);

        return ServiceResult.SuccessResult();
    }

    public async Task<List<UserTenant>> GetUserTenantsAsync(Guid userId)
    {
        return await _context.UserTenants
            .Include(ut => ut.Tenant!)
            .Where(ut => ut.UserId == userId && ut.IsActive)
            .OrderBy(ut => ut.Tenant!.Name)
            .ToListAsync();
    }

    private async Task RevokeUserTokensAsync(Guid userId)
    {
        await foreach (var token in _tokenManager.FindBySubjectAsync(userId.ToString()))
        {
            await _tokenManager.TryRevokeAsync(token);
        }
    }
}
