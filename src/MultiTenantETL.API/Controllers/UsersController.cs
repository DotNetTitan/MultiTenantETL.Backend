using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MultiTenantETL.Application.Common.Interfaces;
using MultiTenantETL.Application.Common.Models;
using MultiTenantETL.Application.Interfaces;
using MultiTenantETL.Application.Tenants.Models;
using MultiTenantETL.Application.Users.Models;
using MultiTenantETL.Domain.Constants;
using MultiTenantETL.Infrastructure.Interfaces;

namespace MultiTenantETL.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly ITenantService _tenantService;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<UsersController> _logger;
    private readonly IAuditService _auditService;

    public UsersController(
        IUserService userService,
        ITenantService tenantService,
        ICurrentUserService currentUserService,
        ILogger<UsersController> logger,
        IAuditService auditService)
    {
        _userService = userService;
        _tenantService = tenantService;
        _currentUserService = currentUserService;
        _logger = logger;
        _auditService = auditService;
    }

    /// <summary>
    /// Get current user's profile
    /// </summary>
    [HttpGet("me")]
    public async Task<IActionResult> GetCurrentUser()
    {
        var userId = _currentUserService.GetUserId();
        var user = await _userService.GetUserByIdAsync(userId);

        if (user == null)
        {
            return NotFound(new ErrorResponse(
                Domain.Enums.AuthErrorCode.UserNotFound,
                "User not found"));
        }

        if (user.Status != Domain.Enums.UserStatus.Active)
        {
            return Unauthorized(new ErrorResponse(
                Domain.Enums.AuthErrorCode.UserInactive,
                "Account is inactive or deleted"));
        }

        var tenants = await _userService.GetUserTenantsAsync(userId);
        var roles = await _userService.GetUserRolesAsync(userId);

        var response = new UserDetailResponse
        {
            Id = user.Id,
            Email = user.Email!,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Status = user.Status,
            EmailConfirmed = user.EmailConfirmed,
            CreatedAt = user.CreatedAt,
            CurrentTenantId = user.CurrentTenantId,
            CurrentTenantName = user.CurrentTenant?.Name,
            Tenants = tenants.Select(ut => new UserTenantInfo
            {
                TenantId = ut.TenantId,
                TenantName = ut.Tenant!.Name!,
                RoleCode = ut.RoleCode,
                IsActive = ut.IsActive
            }).ToList(),
            Roles = roles
        };

        return Ok(response);
    }

    /// <summary>
    /// Update current user's profile
    /// </summary>
    [HttpPut("me")]
    public async Task<IActionResult> UpdateCurrentUser([FromBody] UpdateUserRequest request)
    {
        var userId = _currentUserService.GetUserId();
        var result = await _userService.UpdateUserAsync(
            userId,
            request.FirstName,
            request.LastName,
            request.Email);

        if (!result.Success)
        {
            return BadRequest(new ErrorResponse(result.ErrorCode!.Value, result.ErrorMessage!));
        }

        _logger.LogInformation("User {UserId} updated their profile", userId);

        var response = new UserResponse
        {
            Id = result.Data!.Id,
            Email = result.Data.Email!,
            FirstName = result.Data.FirstName,
            LastName = result.Data.LastName,
            Status = result.Data.Status,
            EmailConfirmed = result.Data.EmailConfirmed,
            CreatedAt = result.Data.CreatedAt,
            CurrentTenantId = result.Data.CurrentTenantId,
            CurrentTenantName = result.Data.CurrentTenant?.Name
        };

        return Ok(response);
    }

    /// <summary>
    /// Search/list users (SuperAdmin, PlatformAdmin, or TenantAdmin)
    /// </summary>
    [HttpGet]
    [Authorize(Roles = $"{Roles.SuperAdmin},{Roles.PlatformAdmin},{Roles.TenantAdmin}")]
    public async Task<IActionResult> GetUsers([FromQuery] UserSearchRequest request)
    {
        var userRole = _currentUserService.GetRole();
        Guid? tenantFilter = null;

        // TenantAdmin can only see users in their tenant
        if (userRole == Roles.TenantAdmin)
        {
            tenantFilter = _currentUserService.GetTenantId();
        }
        else if (request.TenantId.HasValue)
        {
            // SuperAdmin can filter by specific tenant
            tenantFilter = request.TenantId;
        }

        var (users, totalCount) = await _userService.GetUsersAsync(
            request.Email,
            request.Name,
            request.Status,
            tenantFilter,
            request.Page,
            request.PageSize);

        var userResponses = new List<UserResponse>();
        foreach (var u in users)
        {
            var roles = await _userService.GetUserRolesAsync(u.Id);
            userResponses.Add(new UserResponse
            {
                Id = u.Id,
                Email = u.Email!,
                FirstName = u.FirstName,
                LastName = u.LastName,
                Status = u.Status,
                EmailConfirmed = u.EmailConfirmed,
                CreatedAt = u.CreatedAt,
                CurrentTenantId = u.CurrentTenantId,
                CurrentTenantName = u.CurrentTenant?.Name,
                Roles = roles
            });
        }

        var response = new PagedUserResponse
        {
            Users = userResponses,
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize,
            TotalPages = (int)Math.Ceiling(totalCount / (double)request.PageSize)
        };

        return Ok(response);
    }

    /// <summary>
    /// Get user by ID (SuperAdmin, PlatformAdmin, or TenantAdmin for their tenant users)
    /// </summary>
    [HttpGet("{id:guid}")]
    [Authorize(Roles = $"{Roles.SuperAdmin},{Roles.PlatformAdmin},{Roles.TenantAdmin}")]
    public async Task<IActionResult> GetUserById(Guid id)
    {
        var user = await _userService.GetUserByIdAsync(id);

        if (user == null)
        {
            return NotFound(new ErrorResponse(
                Domain.Enums.AuthErrorCode.UserNotFound,
                "User not found"));
        }

        // TenantAdmin can only view users in their tenant
        var userRole = _currentUserService.GetRole();
        if (userRole == Roles.TenantAdmin)
        {
            var currentTenantId = _currentUserService.GetTenantId();
            var userTenants = await _userService.GetUserTenantsAsync(id);

            if (!userTenants.Any(ut => ut.TenantId == currentTenantId))
            {
                return Forbid();
            }
        }

        var tenants = await _userService.GetUserTenantsAsync(id);
        var roles = await _userService.GetUserRolesAsync(id);

        var response = new UserDetailResponse
        {
            Id = user.Id,
            Email = user.Email!,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Status = user.Status,
            EmailConfirmed = user.EmailConfirmed,
            CreatedAt = user.CreatedAt,
            CurrentTenantId = user.CurrentTenantId,
            CurrentTenantName = user.CurrentTenant?.Name,
            Tenants = tenants.Select(ut => new UserTenantInfo
            {
                TenantId = ut.TenantId,
                TenantName = ut.Tenant!.Name!,
                RoleCode = ut.RoleCode,
                IsActive = ut.IsActive
            }).ToList(),
            Roles = roles
        };

        return Ok(response);
    }

    /// <summary>
    /// Update user (SuperAdmin or PlatformAdmin)
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = $"{Roles.SuperAdmin},{Roles.PlatformAdmin}")]
    public async Task<IActionResult> UpdateUser(Guid id, [FromBody] UpdateUserRequest request)
    {
        var currentUserRole = _currentUserService.GetRole();
        var currentUserId = _currentUserService.GetUserId();

        // Non-SuperAdmin users cannot edit SuperAdmin/PlatformAdmin accounts,
        // except they can edit their own profile details.
        if (currentUserRole != Roles.SuperAdmin && id != currentUserId)
        {
            if (await IsSuperAdminUserAsync(id) || await IsPlatformAdminUserAsync(id))
            {
                return Forbid();
            }
        }

        var result = await _userService.UpdateUserAsync(
            id,
            request.FirstName,
            request.LastName,
            request.Email);

        if (!result.Success)
        {
            return BadRequest(new ErrorResponse(result.ErrorCode!.Value, result.ErrorMessage!));
        }

        _logger.LogInformation("User {UserId} updated by admin", id);

        await _auditService.LogAsync(
            Domain.Constants.AuditActions.Users.Updated,
            "User",
            id.ToString(),
            $"User updated: {result.Data!.Email}");

        var response = new UserResponse
        {
            Id = result.Data!.Id,
            Email = result.Data.Email!,
            FirstName = result.Data.FirstName,
            LastName = result.Data.LastName,
            Status = result.Data.Status,
            EmailConfirmed = result.Data.EmailConfirmed,
            CreatedAt = result.Data.CreatedAt,
            CurrentTenantId = result.Data.CurrentTenantId,
            CurrentTenantName = result.Data.CurrentTenant?.Name
        };

        return Ok(response);
    }

    /// <summary>
    /// Update user status (SuperAdmin only)
    /// </summary>
    [HttpPut("{id:guid}/status")]
    [Authorize(Roles = Roles.SuperAdmin)]
    public async Task<IActionResult> UpdateUserStatus(Guid id, [FromBody] UpdateUserStatusRequest request)
    {
        var result = await _userService.UpdateUserStatusAsync(id, request.Status);

        if (!result.Success)
        {
            return BadRequest(new ErrorResponse(result.ErrorCode!.Value, result.ErrorMessage!));
        }

        _logger.LogInformation("User {UserId} status updated to {Status}", id, request.Status);

        await _auditService.LogAsync(
            request.Status == Domain.Enums.UserStatus.Active ? Domain.Constants.AuditActions.Users.Activated : Domain.Constants.AuditActions.Users.Deactivated,
            "User",
            id.ToString(),
            $"User status updated to {request.Status}");

        return Ok(new { message = $"User status updated to {request.Status} successfully" });
    }

    /// <summary>
    /// Delete user (SuperAdmin only)
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = Roles.SuperAdmin)]
    public async Task<IActionResult> DeleteUser(Guid id)
    {
        var result = await _userService.DeleteUserAsync(id);

        if (!result.Success)
        {
            return BadRequest(new ErrorResponse(result.ErrorCode!.Value, result.ErrorMessage!));
        }

        _logger.LogInformation("User {UserId} deleted (soft delete)", id);

        await _auditService.LogAsync(
            Domain.Constants.AuditActions.Users.Deleted,
            "User",
            id.ToString(),
            "User deleted");

        return NoContent();
    }

    /// <summary>
    /// Assign role to user (SuperAdmin only)
    /// </summary>
    [HttpPost("{id:guid}/roles")]
    [Authorize(Roles = Roles.SuperAdmin)]
    public async Task<IActionResult> AssignRole(Guid id, [FromBody] AssignRoleRequest request)
    {
        var result = await _userService.AssignRoleAsync(id, request.RoleName);

        if (!result.Success)
        {
            return BadRequest(new ErrorResponse(result.ErrorCode!.Value, result.ErrorMessage!));
        }

        _logger.LogInformation("Role {RoleName} assigned to user {UserId}", request.RoleName, id);

        await _auditService.LogAsync(
            Domain.Constants.AuditActions.Users.RoleAssigned,
            "User",
            id.ToString(),
            $"Role '{request.RoleName}' assigned to user");

        return Ok(new { message = $"Role '{request.RoleName}' assigned successfully" });
    }

    /// <summary>
    /// Remove role from user (SuperAdmin only)
    /// </summary>
    [HttpDelete("{id:guid}/roles")]
    [Authorize(Roles = Roles.SuperAdmin)]
    public async Task<IActionResult> RemoveRole(Guid id, [FromBody] RemoveRoleRequest request)
    {
        var result = await _userService.RemoveRoleAsync(id, request.RoleName);

        if (!result.Success)
        {
            return BadRequest(new ErrorResponse(result.ErrorCode!.Value, result.ErrorMessage!));
        }

        _logger.LogInformation("Role {RoleName} removed from user {UserId}", request.RoleName, id);

        await _auditService.LogAsync(
            Domain.Constants.AuditActions.Users.RoleRemoved,
            "User",
            id.ToString(),
            $"Role '{request.RoleName}' removed from user");

        return Ok(new { message = $"Role '{request.RoleName}' removed successfully" });
    }

    /// <summary>
    /// Reset user password (SuperAdmin only)
    /// </summary>
    [HttpPost("{id:guid}/reset-password")]
    [Authorize(Roles = Roles.SuperAdmin)]
    public async Task<IActionResult> AdminResetPassword(Guid id, [FromBody] AdminPasswordResetRequest request)
    {
        var result = await _userService.AdminResetPasswordAsync(id, request.NewPassword);

        if (!result.Success)
        {
            return BadRequest(new ErrorResponse(result.ErrorCode!.Value, result.ErrorMessage!));
        }

        _logger.LogInformation("Password reset for user {UserId} by admin", id);

        return Ok(new { message = "Password reset successfully" });
    }

    /// <summary>
    /// Get user's tenant memberships
    /// </summary>
    [HttpGet("{id:guid}/tenants")]
    [Authorize(Roles = $"{Roles.SuperAdmin},{Roles.PlatformAdmin},{Roles.TenantAdmin}")]
    public async Task<IActionResult> GetUserTenants(Guid id)
    {
        var userRole = _currentUserService.GetRole();

        // TenantAdmin can only view users in their tenant
        if (userRole == Roles.TenantAdmin)
        {
            var currentTenantId = _currentUserService.GetTenantId();
            var userTenants = await _userService.GetUserTenantsAsync(id);

            if (!userTenants.Any(ut => ut.TenantId == currentTenantId))
            {
                return Forbid();
            }
        }

        var tenants = await _userService.GetUserTenantsAsync(id);

        var response = tenants.Select(ut => new UserTenantInfo
        {
            TenantId = ut.TenantId,
            TenantName = ut.Tenant!.Name!,
            RoleCode = ut.RoleCode,
            IsActive = ut.IsActive
        }).ToList();

        return Ok(response);
    }

    /// <summary>
    /// Add user to tenant (SuperAdmin or PlatformAdmin)
    /// </summary>
    [HttpPost("{id:guid}/tenants")]
    [Authorize(Roles = $"{Roles.SuperAdmin},{Roles.PlatformAdmin}")]
    public async Task<IActionResult> AddUserToTenant(Guid id, [FromBody] AddUserToTenantRequest request)
    {
        var currentUserRole = _currentUserService.GetRole();
        if (currentUserRole != Roles.SuperAdmin)
        {
            if (await IsSuperAdminUserAsync(id) || await IsPlatformAdminUserAsync(id))
            {
                return Forbid();
            }
        }

        if (request.RoleCode == Roles.SuperAdmin || request.RoleCode == Roles.PlatformAdmin)
        {
            return BadRequest(new ErrorResponse(
                Domain.Enums.AuthErrorCode.ValidationError,
                "Tenant membership role cannot be a global role"));
        }



        var result = await _tenantService.AddUserToTenantAsync(id, request.TenantId, request.RoleCode);

        if (!result.Success)
        {
            return BadRequest(new ErrorResponse(result.ErrorCode!.Value, result.ErrorMessage!));
        }

        _logger.LogInformation("User {UserId} added to tenant {TenantId} with role {RoleCode}",
            id, request.TenantId, request.RoleCode);

        await _auditService.LogAsync(
            Domain.Constants.AuditActions.Users.AddedToTenant,
            "User",
            id.ToString(),
            $"User added to tenant with role {request.RoleCode}");

        return Ok(new { message = "User added to tenant successfully" });
    }

    /// <summary>
    /// Remove user from tenant (SuperAdmin or PlatformAdmin)
    /// </summary>
    [HttpDelete("{userId:guid}/tenants/{tenantId:guid}")]
    [Authorize(Roles = $"{Roles.SuperAdmin},{Roles.PlatformAdmin}")]
    public async Task<IActionResult> RemoveUserFromTenant(Guid userId, Guid tenantId)
    {
        var currentUserRole = _currentUserService.GetRole();
        if (currentUserRole != Roles.SuperAdmin)
        {
            if (await IsSuperAdminUserAsync(userId) || await IsPlatformAdminUserAsync(userId))
            {
                return Forbid();
            }
        }



        var result = await _tenantService.RemoveUserFromTenantAsync(userId, tenantId);

        if (!result.Success)
        {
            return BadRequest(new ErrorResponse(result.ErrorCode!.Value, result.ErrorMessage!));
        }

        _logger.LogInformation("User {UserId} removed from tenant {TenantId}", userId, tenantId);

        await _auditService.LogAsync(
            Domain.Constants.AuditActions.Users.RemovedFromTenant,
            "User",
            userId.ToString(),
            $"User removed from tenant");

        return NoContent();
    }

    /// <summary>
    /// Update user's role in tenant (SuperAdmin or PlatformAdmin)
    /// </summary>
    [HttpPut("{userId:guid}/tenants/{tenantId:guid}/role")]
    [Authorize(Roles = $"{Roles.SuperAdmin},{Roles.PlatformAdmin}")]
    public async Task<IActionResult> UpdateUserTenantRole(Guid userId, Guid tenantId, [FromBody] UpdateUserTenantRoleRequest request)
    {
        var currentUserRole = _currentUserService.GetRole();
        if (currentUserRole != Roles.SuperAdmin)
        {
            if (await IsSuperAdminUserAsync(userId) || await IsPlatformAdminUserAsync(userId))
            {
                return Forbid();
            }
        }

        if (request.RoleCode == Roles.SuperAdmin || request.RoleCode == Roles.PlatformAdmin)
        {
            return BadRequest(new ErrorResponse(
                Domain.Enums.AuthErrorCode.ValidationError,
                "Tenant membership role cannot be a global role"));
        }



        var result = await _tenantService.UpdateUserTenantRoleAsync(userId, tenantId, request.RoleCode);

        if (!result.Success)
        {
            return BadRequest(new ErrorResponse(result.ErrorCode!.Value, result.ErrorMessage!));
        }

        _logger.LogInformation("User {UserId} role updated to {RoleCode} in tenant {TenantId}",
            userId, request.RoleCode, tenantId);

        await _auditService.LogAsync(
            Domain.Constants.AuditActions.Users.TenantRoleUpdated,
            "User",
            userId.ToString(),
            $"User role updated to {request.RoleCode} in tenant");

        return Ok(new { message = "User role updated successfully" });
    }

    private async Task<bool> IsSuperAdminUserAsync(Guid userId)
    {
        var roles = await _userService.GetUserRolesAsync(userId);
        return roles.Contains(Roles.SuperAdmin);
    }

    private async Task<bool> IsPlatformAdminUserAsync(Guid userId)
    {
        var roles = await _userService.GetUserRolesAsync(userId);
        return roles.Contains(Roles.PlatformAdmin);
    }
}
