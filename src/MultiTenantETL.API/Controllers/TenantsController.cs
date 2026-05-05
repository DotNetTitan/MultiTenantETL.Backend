using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MultiTenantETL.Application.Common.Interfaces;
using MultiTenantETL.Application.Common.Models;
using MultiTenantETL.Application.Interfaces;
using MultiTenantETL.Application.Tenants.Models;
using MultiTenantETL.Domain.Constants;
using MultiTenantETL.Infrastructure.Interfaces;

namespace MultiTenantETL.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TenantsController : ControllerBase
{
    private readonly ITenantService _tenantService;
    private readonly IUserService _userService;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<TenantsController> _logger;
    private readonly IAuditService _auditService;

    public TenantsController(
        ITenantService tenantService,
        IUserService userService,
        ICurrentUserService currentUserService,
        ILogger<TenantsController> logger,
        IAuditService auditService)
    {
        _tenantService = tenantService;
        _userService = userService;
        _currentUserService = currentUserService;
        _logger = logger;
        _auditService = auditService;
    }

    /// <summary>
    /// Get all tenants (SuperAdmin or PlatformAdmin)
    /// </summary>
    [HttpGet]
    [Authorize(Roles = $"{Roles.SuperAdmin},{Roles.PlatformAdmin}")]
    public async Task<IActionResult> GetAllTenants()
    {
        var tenants = await _tenantService.GetAllTenantsAsync();

        var response = tenants.Select(t => new TenantResponse
        {
            Id = t.Id,
            Name = t.Name,
            Slug = t.Slug,
            Status = t.Status,
            CreatedAt = t.CreatedAt
        });

        return Ok(response);
    }

    /// <summary>
    /// Get current user's tenants
    /// </summary>
    [HttpGet("my-tenants")]
    public async Task<IActionResult> GetMyTenants()
    {
        var userId = _currentUserService.GetUserId();
        var userTenants = await _tenantService.GetUserTenantsAsync(userId);
        var currentTenantId = _currentUserService.GetTenantId();

        var response = userTenants.Select(ut => new UserTenantResponse
        {
            TenantId = ut.TenantId,
            TenantName = ut.Tenant!.Name!,
            TenantSlug = ut.Tenant!.Slug!,
            RoleCode = ut.RoleCode,
            Status = ut.Tenant!.Status,
            IsActive = ut.IsActive,
            IsCurrent = ut.TenantId == currentTenantId
        });

        return Ok(response);
    }

    /// <summary>
    /// Get tenant by ID
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetTenantById(Guid id)
    {
        var tenant = await _tenantService.GetTenantByIdAsync(id);

        if (tenant == null)
        {
            return NotFound(new ErrorResponse(
                Domain.Enums.AuthErrorCode.TenantNotFound,
                "Tenant not found"));
        }

        // Check if user has access to this tenant (unless SuperAdmin/PlatformAdmin)
        var userId = _currentUserService.GetUserId();
        var userRole = _currentUserService.GetRole();

        if (userRole != Roles.SuperAdmin && userRole != Roles.PlatformAdmin)
        {
            var userTenants = await _tenantService.GetUserTenantsAsync(userId);
            if (!userTenants.Any(ut => ut.TenantId == id))
            {
                return Forbid();
            }
        }

        var response = new TenantResponse
        {
            Id = tenant.Id,
            Name = tenant.Name,
            Slug = tenant.Slug,
            Status = tenant.Status,
            CreatedAt = tenant.CreatedAt
        };

        return Ok(response);
    }

    /// <summary>
    /// Create a new tenant (SuperAdmin or PlatformAdmin)
    /// </summary>
    [HttpPost]
    [Authorize(Roles = $"{Roles.SuperAdmin},{Roles.PlatformAdmin}")]
    public async Task<IActionResult> CreateTenant([FromBody] CreateTenantRequest request)
    {
        var result = await _tenantService.CreateTenantAsync(request.Name, request.Slug);

        if (!result.Success)
        {
            return BadRequest(new ErrorResponse(result.ErrorCode!.Value, result.ErrorMessage!));
        }

        _logger.LogInformation("Tenant {TenantName} created with slug {Slug}", request.Name, request.Slug);

        await _auditService.LogAsync(
            Domain.Constants.AuditActions.Tenants.Created,
            "Tenant",
            result.Data!.Id.ToString(),
            $"Tenant created: {request.Name}");

        var response = new TenantResponse
        {
            Id = result.Data!.Id,
            Name = result.Data.Name,
            Slug = result.Data.Slug,
            Status = result.Data.Status,
            CreatedAt = result.Data.CreatedAt
        };

        return CreatedAtAction(nameof(GetTenantById), new { id = response.Id }, response);
    }

    /// <summary>
    /// Update a tenant (SuperAdmin, PlatformAdmin, or TenantAdmin)
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = $"{Roles.SuperAdmin},{Roles.PlatformAdmin},{Roles.TenantAdmin}")]
    public async Task<IActionResult> UpdateTenant(Guid id, [FromBody] UpdateTenantRequest request)
    {
        // TenantAdmin can only update their own tenant
        var userRole = _currentUserService.GetRole();
        if (userRole == Roles.TenantAdmin)
        {
            var currentTenantId = _currentUserService.GetTenantId();
            if (id != currentTenantId)
            {
                return Forbid();
            }
        }

        var result = await _tenantService.UpdateTenantAsync(id, request.Name, request.Status);

        if (!result.Success)
        {
            return BadRequest(new ErrorResponse(result.ErrorCode!.Value, result.ErrorMessage!));
        }

        _logger.LogInformation("Tenant {TenantId} updated", id);

        await _auditService.LogAsync(
            Domain.Constants.AuditActions.Tenants.Updated,
            "Tenant",
            id.ToString(),
            $"Tenant updated: {result.Data!.Name}");

        var response = new TenantResponse
        {
            Id = result.Data!.Id,
            Name = result.Data.Name,
            Slug = result.Data.Slug,
            Status = result.Data.Status,
            CreatedAt = result.Data.CreatedAt
        };

        return Ok(response);
    }

    /// <summary>
    /// Delete a tenant (SuperAdmin only)
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = Roles.SuperAdmin)]
    public async Task<IActionResult> DeleteTenant(Guid id)
    {
        var result = await _tenantService.DeleteTenantAsync(id);

        if (!result.Success)
        {
            return BadRequest(new ErrorResponse(result.ErrorCode!.Value, result.ErrorMessage!));
        }

        _logger.LogInformation("Tenant {TenantId} deleted (soft delete)", id);

        await _auditService.LogAsync(
            Domain.Constants.AuditActions.Tenants.Deleted,
            "Tenant",
            id.ToString(),
            "Tenant deleted");

        return NoContent();
    }

    /// <summary>
    /// Get all users in a tenant (SuperAdmin, PlatformAdmin, or TenantAdmin)
    /// </summary>
    [HttpGet("{id:guid}/users")]
    [Authorize(Roles = $"{Roles.SuperAdmin},{Roles.PlatformAdmin},{Roles.TenantAdmin}")]
    public async Task<IActionResult> GetTenantUsers(Guid id)
    {
        // TenantAdmin can only view their own tenant's users
        var userRole = _currentUserService.GetRole();
        if (userRole == Roles.TenantAdmin)
        {
            var currentTenantId = _currentUserService.GetTenantId();
            if (id != currentTenantId)
            {
                return Forbid();
            }
        }

        var userTenants = await _tenantService.GetTenantUsersAsync(id);

        var response = userTenants.Select(ut => new TenantUserResponse
        {
            UserId = ut.UserId,
            Email = ut.User!.Email!,
            FirstName = ut.User!.FirstName!,
            LastName = ut.User!.LastName!,
            RoleCode = ut.RoleCode,
            Status = ut.User!.Status,
            IsActive = ut.IsActive
        });

        return Ok(response);
    }

    /// <summary>
    /// Add a user to a tenant (SuperAdmin, PlatformAdmin, or TenantAdmin)
    /// </summary>
    [HttpPost("{id:guid}/users")]
    [Authorize(Roles = $"{Roles.SuperAdmin},{Roles.PlatformAdmin},{Roles.TenantAdmin}")]
    public async Task<IActionResult> AddUserToTenant(Guid id, [FromBody] AddUserToTenantRequest request)
    {
        var currentUserRole = _currentUserService.GetRole();
        if (currentUserRole != Roles.SuperAdmin && await IsSuperAdminUserAsync(request.UserId))
        {
            return Forbid();
        }

        if (request.RoleCode == Roles.SuperAdmin || request.RoleCode == Roles.PlatformAdmin)
        {
            return BadRequest(new ErrorResponse(
                Domain.Enums.AuthErrorCode.ValidationError,
                "Tenant membership role cannot be a global role"));
        }

        // Validate tenant ID matches route
        if (id != request.TenantId)
        {
            return BadRequest(new ErrorResponse(
                Domain.Enums.AuthErrorCode.ValidationError,
                "Tenant ID in route must match tenant ID in request body"));
        }

        // TenantAdmin can only add users to their own tenant
        if (currentUserRole == Roles.TenantAdmin)
        {
            var currentTenantId = _currentUserService.GetTenantId();
            if (id != currentTenantId)
            {
                return Forbid();
            }
        }

        var result = await _tenantService.AddUserToTenantAsync(
            request.UserId,
            request.TenantId,
            request.RoleCode);

        if (!result.Success)
        {
            return BadRequest(new ErrorResponse(result.ErrorCode!.Value, result.ErrorMessage!));
        }

        _logger.LogInformation(
            "User {UserId} added to tenant {TenantId} with role {RoleCode}",
            request.UserId,
            request.TenantId,
            request.RoleCode);

        await _auditService.LogAsync(
            Domain.Constants.AuditActions.Tenants.UserAdded,
            "Tenant",
            request.TenantId.ToString(),
            $"User added to tenant with role {request.RoleCode}");

        return Ok(new
        {
            userId = result.Data!.UserId,
            tenantId = result.Data.TenantId,
            roleCode = result.Data.RoleCode,
            message = "User added to tenant successfully"
        });
    }

    /// <summary>
    /// Remove a user from a tenant (SuperAdmin, PlatformAdmin, or TenantAdmin)
    /// </summary>
    [HttpDelete("{tenantId:guid}/users/{userId:guid}")]
    [Authorize(Roles = $"{Roles.SuperAdmin},{Roles.PlatformAdmin},{Roles.TenantAdmin}")]
    public async Task<IActionResult> RemoveUserFromTenant(Guid tenantId, Guid userId)
    {
        var currentUserRole = _currentUserService.GetRole();
        if (currentUserRole != Roles.SuperAdmin && await IsSuperAdminUserAsync(userId))
        {
            return Forbid();
        }

        // TenantAdmin can only remove users from their own tenant
        if (currentUserRole == Roles.TenantAdmin)
        {
            var currentTenantId = _currentUserService.GetTenantId();
            if (tenantId != currentTenantId)
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
            Domain.Constants.AuditActions.Tenants.UserRemoved,
            "Tenant",
            tenantId.ToString(),
            "User removed from tenant");

        return NoContent();
    }

    /// <summary>
    /// Update a user's role within a tenant (SuperAdmin, PlatformAdmin, or TenantAdmin)
    /// </summary>
    [HttpPut("{tenantId:guid}/users/{userId:guid}/role")]
    [Authorize(Roles = $"{Roles.SuperAdmin},{Roles.PlatformAdmin},{Roles.TenantAdmin}")]
    public async Task<IActionResult> UpdateUserTenantRole(
        Guid tenantId,
        Guid userId,
        [FromBody] UpdateUserTenantRoleRequest request)
    {
        var currentUserRole = _currentUserService.GetRole();
        if (currentUserRole != Roles.SuperAdmin && await IsSuperAdminUserAsync(userId))
        {
            return Forbid();
        }

        if (request.RoleCode == Roles.SuperAdmin || request.RoleCode == Roles.PlatformAdmin)
        {
            return BadRequest(new ErrorResponse(
                Domain.Enums.AuthErrorCode.ValidationError,
                "Tenant membership role cannot be a global role"));
        }

        // TenantAdmin can only update roles in their own tenant
        if (currentUserRole == Roles.TenantAdmin)
        {
            var currentTenantId = _currentUserService.GetTenantId();
            if (tenantId != currentTenantId)
            {
                return Forbid();
            }
        }

        var result = await _tenantService.UpdateUserTenantRoleAsync(userId, tenantId, request.RoleCode);

        if (!result.Success)
        {
            return BadRequest(new ErrorResponse(result.ErrorCode!.Value, result.ErrorMessage!));
        }

        _logger.LogInformation(
            "User {UserId} role updated to {RoleCode} in tenant {TenantId}",
            userId,
            request.RoleCode,
            tenantId);

        await _auditService.LogAsync(
            Domain.Constants.AuditActions.Tenants.UserRoleUpdated,
            "Tenant",
            tenantId.ToString(),
            $"User role updated to {request.RoleCode}");

        return Ok(new
        {
            userId = result.Data!.UserId,
            tenantId = result.Data.TenantId,
            roleCode = result.Data.RoleCode,
            message = "User role updated successfully"
        });
    }

    private async Task<bool> IsSuperAdminUserAsync(Guid userId)
    {
        var roles = await _userService.GetUserRolesAsync(userId);
        return roles.Contains(Roles.SuperAdmin);
    }
}
