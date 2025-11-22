using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MultiTenantETL.Domain.Enums;
using MultiTenantETL.Infrastructure.Identity;
using MultiTenantETL.Infrastructure.Interfaces;
using MultiTenantETL.Infrastructure.Persistence;

namespace MultiTenantETL.Infrastructure.Services;

public class TenantService : ITenantService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ApplicationDbContext _context;

    public TenantService(
        UserManager<ApplicationUser> userManager,
        ApplicationDbContext context)
    {
        _userManager = userManager;
        _context = context;
    }

    public async Task<TenantSwitchResult> SwitchUserTenantAsync(Guid userId, Guid tenantId)
    {
        // Validate user has access to the tenant
        var userTenant = await _context.UserTenants
            .Include(ut => ut.Tenant)
            .FirstOrDefaultAsync(ut =>
                ut.UserId == userId &&
                ut.TenantId == tenantId &&
                ut.IsActive);

        if (userTenant == null)
        {
            return new TenantSwitchResult
            {
                Success = false,
                ErrorCode = AuthErrorCode.TenantAccessDenied,
                ErrorMessage = "You don't have access to this tenant"
            };
        }

        // Update user's current tenant
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            return new TenantSwitchResult
            {
                Success = false,
                ErrorCode = AuthErrorCode.UserNotFound,
                ErrorMessage = "User not found"
            };
        }

        user.CurrentTenantId = tenantId;
        await _userManager.UpdateAsync(user);

        return new TenantSwitchResult
        {
            Success = true,
            UserTenant = userTenant
        };
    }
}
