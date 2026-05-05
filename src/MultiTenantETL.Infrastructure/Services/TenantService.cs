using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MultiTenantETL.Application.Common.Models;
using MultiTenantETL.Application.Tenants.Models;
using MultiTenantETL.Domain.Entities;
using MultiTenantETL.Domain.Enums;
using MultiTenantETL.Infrastructure.Identity;
using MultiTenantETL.Infrastructure.Interfaces;
using MultiTenantETL.Infrastructure.Persistence;
using MultiTenantETL.Application.Common.Interfaces;

namespace MultiTenantETL.Infrastructure.Services;

public class TenantService : ITenantService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public TenantService(
        UserManager<ApplicationUser> userManager,
        ApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _userManager = userManager;
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<ServiceResult<Tenant>> CreateTenantAsync(string name, string slug)
    {
        // Check if slug already exists
        var existingTenant = await _context.Tenants
            .IgnoreQueryFilters()
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
            Status = TenantStatus.Active,
            CreatedAt = DateTime.UtcNow
        };

        _context.Tenants.Add(tenant);
        await _context.SaveChangesAsync();

        return ServiceResult<Tenant>.SuccessResult(tenant);
    }

    public async Task<Tenant?> GetTenantByIdAsync(Guid tenantId)
    {
        return await _context.Tenants
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == tenantId);
    }

    public async Task<Tenant?> GetTenantBySlugAsync(string slug)
    {
        return await _context.Tenants
            .IgnoreQueryFilters()
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
            .Include(ut => ut.Tenant!)
            .Where(ut => ut.UserId == userId && ut.IsActive)
            .OrderBy(ut => ut.Tenant!.Name)
            .ToListAsync();
    }

    public async Task<ServiceResult<Tenant>> UpdateTenantAsync(Guid tenantId, string name, TenantStatus? status)
    {
        var tenant = await _context.Tenants.FindAsync(tenantId);
        if (tenant == null)
        {
            return ServiceResult<Tenant>.FailureResult(
                AuthErrorCode.TenantNotFound,
                "Tenant not found");
        }

        tenant.Name = name;
        if (status.HasValue)
        {
            tenant.Status = status.Value;
        }

        await _context.SaveChangesAsync();

        return ServiceResult<Tenant>.SuccessResult(tenant);
    }

    public async Task<ServiceResult> DeleteTenantAsync(Guid tenantId)
    {
        var tenant = await _context.Tenants
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == tenantId);

        if (tenant == null)
        {
            return ServiceResult.FailureResult(
                AuthErrorCode.TenantNotFound,
                "Tenant not found");
        }

        if (tenant.Status == TenantStatus.Deleted)
        {
            return ServiceResult.SuccessResult(); // Already deleted, consider it a success
        }

        // Irreversible delete logic
        tenant.Status = TenantStatus.Deleted;
        tenant.DeletedAt = DateTime.UtcNow;
        tenant.DeletedBy = _currentUserService.GetUserId();
        
        // Scramble slug to allow reuse
        tenant.Slug = $"{tenant.Slug}_deleted_{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
        
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
            .OrderBy(ut => ut.User!.Email!)
            .ToListAsync();
    }

    public async Task<TenantSwitchResult> SwitchUserTenantAsync(Guid userId, Guid tenantId)
    {
        // Get user first to check if they're SuperAdmin
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

        // Check if user is SuperAdmin
        var roles = await _userManager.GetRolesAsync(user);
        var isSuperAdmin = roles.Contains(Domain.Constants.Roles.SuperAdmin);

        // Validate tenant exists
        var tenant = await _context.Tenants
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == tenantId && t.Status == TenantStatus.Active);

        if (tenant == null)
        {
            return new TenantSwitchResult
            {
                Success = false,
                ErrorCode = AuthErrorCode.TenantNotFound,
                ErrorMessage = "Tenant not found or inactive"
            };
        }

        UserTenant? userTenant = null;

        // SuperAdmin can switch to any tenant without being a member
        if (!isSuperAdmin)
        {
            // Regular users must be a member of the tenant
            userTenant = await _context.UserTenants
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
        }
        else
        {
            // For SuperAdmin, create a virtual UserTenant for the response
            userTenant = new UserTenant
            {
                UserId = userId,
                TenantId = tenantId,
                RoleCode = Domain.Constants.Roles.SuperAdmin,
                IsActive = true,
                Tenant = tenant,
                User = user
            };
        }

        // Update user's current tenant
        user.CurrentTenantId = tenantId;
        await _userManager.UpdateAsync(user);

        return new TenantSwitchResult
        {
            Success = true,
            UserTenant = new UserTenantResponse
            {
                TenantId = userTenant.TenantId,
                TenantName = userTenant!.Tenant!.Name!,
                TenantSlug = userTenant!.Tenant!.Slug!,
                RoleCode = userTenant.RoleCode,
                IsActive = userTenant.IsActive,
                IsCurrent = true
            }
        };
    }
}
