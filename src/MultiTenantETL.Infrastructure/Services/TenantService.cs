using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MultiTenantETL.Domain.Entities;
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

    public async Task<ServiceResult<Tenant>> CreateTenantAsync(string name, string slug)
    {
        // Check if slug already exists
        var existingTenant = await _context.Tenants
            .FirstOrDefaultAsync(t => t.Slug == slug);

        if (existingTenant != null)
        {
            return ServiceResult<Tenant>.FailureResult(
                AuthErrorCode.TenantAlreadyExists,
                "A tenant with this slug already exists");
        }

        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = name,
            Slug = slug,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _context.Tenants.Add(tenant);
        await _context.SaveChangesAsync();

        return ServiceResult<Tenant>.SuccessResult(tenant);
    }

    public async Task<Tenant?> GetTenantByIdAsync(Guid tenantId)
    {
        return await _context.Tenants
            .FirstOrDefaultAsync(t => t.Id == tenantId);
    }

    public async Task<Tenant?> GetTenantBySlugAsync(string slug)
    {
        return await _context.Tenants
            .FirstOrDefaultAsync(t => t.Slug == slug);
    }

    public async Task<List<Tenant>> GetAllTenantsAsync()
    {
        return await _context.Tenants
            .OrderBy(t => t.Name)
            .ToListAsync();
    }

    public async Task<List<UserTenant>> GetUserTenantsAsync(Guid userId)
    {
        return await _context.UserTenants
            .Include(ut => ut.Tenant)
            .Where(ut => ut.UserId == userId && ut.IsActive)
            .OrderBy(ut => ut.Tenant.Name)
            .ToListAsync();
    }

    public async Task<ServiceResult<Tenant>> UpdateTenantAsync(Guid tenantId, string name, bool? isActive)
    {
        var tenant = await _context.Tenants.FindAsync(tenantId);
        if (tenant == null)
        {
            return ServiceResult<Tenant>.FailureResult(
                AuthErrorCode.TenantNotFound,
                "Tenant not found");
        }

        tenant.Name = name;
        if (isActive.HasValue)
        {
            tenant.IsActive = isActive.Value;
        }

        await _context.SaveChangesAsync();

        return ServiceResult<Tenant>.SuccessResult(tenant);
    }

    public async Task<ServiceResult> DeleteTenantAsync(Guid tenantId)
    {
        var tenant = await _context.Tenants.FindAsync(tenantId);
        if (tenant == null)
        {
            return ServiceResult.FailureResult(
                AuthErrorCode.TenantNotFound,
                "Tenant not found");
        }

        // Soft delete
        tenant.IsActive = false;
        await _context.SaveChangesAsync();

        return ServiceResult.SuccessResult();
    }

    public async Task<ServiceResult<UserTenant>> AddUserToTenantAsync(Guid userId, Guid tenantId, string roleCode)
    {
        // Validate user exists
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            return ServiceResult<UserTenant>.FailureResult(
                AuthErrorCode.UserNotFound,
                "User not found");
        }

        // Validate tenant exists
        var tenant = await _context.Tenants.FindAsync(tenantId);
        if (tenant == null)
        {
            return ServiceResult<UserTenant>.FailureResult(
                AuthErrorCode.TenantNotFound,
                "Tenant not found");
        }

        // Check if user is already in tenant
        var existingUserTenant = await _context.UserTenants
            .FirstOrDefaultAsync(ut => ut.UserId == userId && ut.TenantId == tenantId);

        if (existingUserTenant != null)
        {
            return ServiceResult<UserTenant>.FailureResult(
                AuthErrorCode.UserAlreadyInTenant,
                "User is already a member of this tenant");
        }

        var userTenant = new UserTenant
        {
            UserId = userId,
            TenantId = tenantId,
            RoleCode = roleCode,
            IsActive = true
        };

        _context.UserTenants.Add(userTenant);

        // If user has no current tenant, set this as their current tenant
        if (user.CurrentTenantId == null)
        {
            user.CurrentTenantId = tenantId;
            await _userManager.UpdateAsync(user);
        }

        await _context.SaveChangesAsync();

        // Reload with navigation properties
        await _context.Entry(userTenant)
            .Reference(ut => ut.Tenant)
            .LoadAsync();
        await _context.Entry(userTenant)
            .Reference(ut => ut.User)
            .LoadAsync();

        return ServiceResult<UserTenant>.SuccessResult(userTenant);
    }

    public async Task<ServiceResult> RemoveUserFromTenantAsync(Guid userId, Guid tenantId)
    {
        var userTenant = await _context.UserTenants
            .FirstOrDefaultAsync(ut => ut.UserId == userId && ut.TenantId == tenantId);

        if (userTenant == null)
        {
            return ServiceResult.FailureResult(
                AuthErrorCode.UserNotInTenant,
                "User is not a member of this tenant");
        }

        _context.UserTenants.Remove(userTenant);

        // If this was the user's current tenant, clear it
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user?.CurrentTenantId == tenantId)
        {
            user.CurrentTenantId = null;
            await _userManager.UpdateAsync(user);
        }

        await _context.SaveChangesAsync();

        return ServiceResult.SuccessResult();
    }

    public async Task<ServiceResult<UserTenant>> UpdateUserTenantRoleAsync(Guid userId, Guid tenantId, string roleCode)
    {
        var userTenant = await _context.UserTenants
            .Include(ut => ut.Tenant)
            .Include(ut => ut.User)
            .FirstOrDefaultAsync(ut => ut.UserId == userId && ut.TenantId == tenantId);

        if (userTenant == null)
        {
            return ServiceResult<UserTenant>.FailureResult(
                AuthErrorCode.UserNotInTenant,
                "User is not a member of this tenant");
        }

        userTenant.RoleCode = roleCode;
        await _context.SaveChangesAsync();

        return ServiceResult<UserTenant>.SuccessResult(userTenant);
    }

    public async Task<List<UserTenant>> GetTenantUsersAsync(Guid tenantId)
    {
        return await _context.UserTenants
            .Include(ut => ut.User)
            .Where(ut => ut.TenantId == tenantId && ut.IsActive)
            .OrderBy(ut => ut.User.Email)
            .ToListAsync();
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
