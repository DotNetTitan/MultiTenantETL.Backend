using MultiTenantETL.Application.Common.Interfaces;
using MultiTenantETL.Domain.Constants;
using MultiTenantETL.Infrastructure.Interfaces;

namespace MultiTenantETL.API.Authorization;

public class AdminAuthorizationService : IAdminAuthorizationService
{
    private readonly IUserService _userService;
    private readonly ICurrentUserService _currentUserService;

    public AdminAuthorizationService(
        IUserService userService,
        ICurrentUserService currentUserService)
    {
        _userService = userService;
        _currentUserService = currentUserService;
    }

    public async Task<bool> IsGlobalAdminAsync()
    {
        var roles = await GetCurrentUserRolesAsync();
        return roles.Contains(Roles.SuperAdmin) || roles.Contains(Roles.PlatformAdmin);
    }

    public async Task<bool> IsSuperAdminAsync()
    {
        var roles = await GetCurrentUserRolesAsync();
        return roles.Contains(Roles.SuperAdmin);
    }

    public async Task<bool> IsSuperAdminUserAsync(Guid userId)
    {
        var roles = await _userService.GetUserRolesAsync(userId);
        return roles.Contains(Roles.SuperAdmin);
    }

    public async Task<bool> IsPlatformAdminUserAsync(Guid userId)
    {
        var roles = await _userService.GetUserRolesAsync(userId);
        return roles.Contains(Roles.PlatformAdmin);
    }

    public async Task<bool> IsProtectedGlobalAdminTargetAsync(Guid userId)
    {
        return await IsSuperAdminUserAsync(userId) || await IsPlatformAdminUserAsync(userId);
    }

    public async Task<bool> CanMutateGlobalAdminTargetAsync(Guid targetUserId, bool allowSelf = false)
    {
        if (await IsSuperAdminAsync())
        {
            return true;
        }

        var currentUserId = _currentUserService.GetUserId();
        if (allowSelf && currentUserId == targetUserId)
        {
            return true;
        }

        return !await IsProtectedGlobalAdminTargetAsync(targetUserId);
    }

    private Task<List<string>> GetCurrentUserRolesAsync()
    {
        var userId = _currentUserService.GetUserId();
        return _userService.GetUserRolesAsync(userId);
    }
}
