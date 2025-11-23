using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MultiTenantETL.Application.Common.Interfaces;
using MultiTenantETL.Application.Common.Models;
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

    public UsersController(
        IUserService userService,
        ITenantService tenantService,
        ICurrentUserService currentUserService,
        ILogger<UsersController> logger)
    {
        _userService = userService;
        _tenantService = tenantService;
        _currentUserService = currentUserService;
        _logger = logger;
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

        var tenants = await _userService.GetUserTenantsAsync(userId);
        var roles = await _userService.GetUserRolesAsync(userId);

        var response = new UserDetailResponse
        {
            Id = user.Id,
            Email = user.Email!,
            FirstName = user.FirstName,
            LastName = user.LastName,
            IsActive = user.IsActive,
            EmailConfirmed = user.EmailConfirmed,
            CreatedAt = user.CreatedAt,
            CurrentTenantId = user.CurrentTenantId,
            CurrentTenantName = user.CurrentTenant?.Name,
            Tenants = tenants.Select(ut => new UserTenantInfo
            {
                TenantId = ut.TenantId,
                TenantName = ut.Tenant.Name,
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
            IsActive = result.Data.IsActive,
            EmailConfirmed = result.Data.EmailConfirmed,
            CreatedAt = result.Data.CreatedAt,
            CurrentTenantId = result.Data.CurrentTenantId,
            CurrentTenantName = result.Data.CurrentTenant?.Name
        };

        return Ok(response);
    }

    /// <summary>
    /// Search/list users (SuperAdmin or TenantAdmin)
    /// </summary>
    [HttpGet]
    [Authorize(Roles = $"{Roles.SuperAdmin},{Roles.TenantAdmin}")]
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
            request.IsActive,
            tenantFilter,
            request.Page,
            request.PageSize);

        var userResponses = users.Select(u => new UserResponse
        {
            Id = u.Id,
            Email = u.Email!,
            FirstName = u.FirstName,
            LastName = u.LastName,
            IsActive = u.IsActive,
            EmailConfirmed = u.EmailConfirmed,
            CreatedAt = u.CreatedAt,
            CurrentTenantId = u.CurrentTenantId,
            CurrentTenantName = u.CurrentTenant?.Name
        }).ToList();

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
    /// Get user by ID (SuperAdmin or TenantAdmin for their tenant users)
    /// </summary>
    [HttpGet("{id:guid}")]
    [Authorize(Roles = $"{Roles.SuperAdmin},{Roles.TenantAdmin}")]
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
            IsActive = user.IsActive,
            EmailConfirmed = user.EmailConfirmed,
            CreatedAt = user.CreatedAt,
            CurrentTenantId = user.CurrentTenantId,
            CurrentTenantName = user.CurrentTenant?.Name,
            Tenants = tenants.Select(ut => new UserTenantInfo
            {
                TenantId = ut.TenantId,
                TenantName = ut.Tenant.Name,
                RoleCode = ut.RoleCode,
                IsActive = ut.IsActive
            }).ToList(),
            Roles = roles
        };

        return Ok(response);
    }

    /// <summary>
    /// Update user (SuperAdmin only)
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = Roles.SuperAdmin)]
    public async Task<IActionResult> UpdateUser(Guid id, [FromBody] UpdateUserRequest request)
    {
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

        var response = new UserResponse
        {
            Id = result.Data!.Id,
            Email = result.Data.Email!,
            FirstName = result.Data.FirstName,
            LastName = result.Data.LastName,
            IsActive = result.Data.IsActive,
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
        var result = await _userService.UpdateUserStatusAsync(id, request.IsActive);

        if (!result.Success)
        {
            return BadRequest(new ErrorResponse(result.ErrorCode!.Value, result.ErrorMessage!));
        }

        _logger.LogInformation("User {UserId} status updated to {IsActive}", id, request.IsActive);

        return Ok(new { message = $"User {(request.IsActive ? "activated" : "deactivated")} successfully" });
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
}
