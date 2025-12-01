using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using MultiTenantETL.Application.Common.Interfaces;
using CustomClaims = MultiTenantETL.Domain.Constants.ClaimTypes;

namespace MultiTenantETL.Infrastructure.Identity;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ITenantProvider _tenantProvider;

    public CurrentUserService(
        IHttpContextAccessor httpContextAccessor,
        ITenantProvider tenantProvider)
    {
        _httpContextAccessor = httpContextAccessor;
        _tenantProvider = tenantProvider;
    }

    private ClaimsPrincipal? User => _httpContextAccessor.HttpContext?.User;

    public Guid GetUserId()
    {
        var userIdClaim = User?.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                          ?? User?.FindFirst("sub")?.Value;
        return Guid.TryParse(userIdClaim, out var userId) ? userId : Guid.Empty;
    }

    public Guid GetTenantId()
    {
        // First try to get from HTTP context claims (web requests)
        var tenantIdClaim = User?.FindFirst(CustomClaims.TenantId)?.Value;
        if (Guid.TryParse(tenantIdClaim, out var tenantId))
            return tenantId;

        // Fallback to tenant provider for non-HTTP contexts (background workers)
        return _tenantProvider.TenantId ?? Guid.Empty;
    }

    public string GetRole()
    {
        return User?.FindFirst(ClaimTypes.Role)?.Value ?? MultiTenantETL.Domain.Constants.Roles.User;
    }

    public string GetEmail()
    {
        return User?.FindFirst(ClaimTypes.Email)?.Value 
               ?? User?.FindFirst("email")?.Value 
               ?? string.Empty;
    }

    public string GetName()
    {
        return User?.FindFirst(ClaimTypes.Name)?.Value 
               ?? User?.FindFirst("name")?.Value 
               ?? string.Empty;
    }

    public string GetTenantName()
    {
        return User?.FindFirst(CustomClaims.TenantName)?.Value ?? string.Empty;
    }

    public List<string> GetPermissions()
    {
        // Try to get from JSON claim first (more efficient)
        var permissionsJson = User?.FindFirst(CustomClaims.Permissions)?.Value;
        if (!string.IsNullOrEmpty(permissionsJson))
        {
            try
            {
                return JsonSerializer.Deserialize<List<string>>(permissionsJson) ?? new List<string>();
            }
            catch (JsonException)
            {
                // Fall through to individual claims
            }
        }

        // Fallback: get individual permission claims
        return User?.FindAll(CustomClaims.Permission).Select(c => c.Value).ToList() ?? new List<string>();
    }

    public bool HasPermission(string permission)
    {
        if (string.IsNullOrWhiteSpace(permission))
            return false;

        var permissions = GetPermissions();

        // Check exact permission
        if (permissions.Contains(permission))
            return true;

        // Check wildcard permissions
        var parts = permission.Split(':');
        if (parts.Length == 2)
        {
            var resource = parts[0];
            var action = parts[1];

            // Check {resource}:* (e.g., "pipelines:*")
            if (permissions.Contains($"{resource}:*"))
                return true;

            // Check *:{action} (e.g., "*:read")
            if (permissions.Contains($"*:{action}"))
                return true;

            // Check super admin (*:*)
            if (permissions.Contains("*:*"))
                return true;
        }

        return false;
    }

    public bool IsInRole(string role)
    {
        if (string.IsNullOrWhiteSpace(role))
            return false;

        return User?.IsInRole(role) ?? false;
    }
}