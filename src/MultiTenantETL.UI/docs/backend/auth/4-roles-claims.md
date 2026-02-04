# 4. Roles & Claims

**Purpose:** Role management, JWT claims population, and permission checking

---

## Overview

The authentication system integrates with authorization through roles and claims. Every authenticated user has:
- **Role**: Defines their permission level (Admin, Manager, User)
- **Claims**: Metadata in JWT token (tenant_id, permissions, role, etc.)

---

## User Service (Role Management)

```csharp
// Infrastructure/Identity/UserService.cs
public class UserService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ApplicationDbContext _context;
    private readonly RoleManager<ApplicationRole> _roleManager;

    public UserService(
        UserManager<ApplicationUser> userManager,
        ApplicationDbContext context,
        RoleManager<ApplicationRole> roleManager)
    {
        _userManager = userManager;
        _context = context;
        _roleManager = roleManager;
    }

    public async Task AssignRoleToUserAsync(Guid userId, Guid tenantId, string roleCode)
    {
        // Validate role exists
        var role = await _context.Roles.FirstOrDefaultAsync(r => r.Name == roleCode);
        if (role == null)
            throw new InvalidOperationException($"Role {roleCode} does not exist");

        // Update UserTenant relationship
        var userTenant = await _context.UserTenants
            .FirstOrDefaultAsync(ut => ut.UserId == userId && ut.TenantId == tenantId);
        
        if (userTenant == null)
        {
            // Create new tenant membership
            userTenant = new UserTenant
            {
                UserId = userId,
                TenantId = tenantId,
                RoleCode = roleCode,
                IsActive = true
            };
            _context.UserTenants.Add(userTenant);
        }
        else
        {
            // Update existing membership
            userTenant.RoleCode = roleCode;
        }

        await _context.SaveChangesAsync();
    }

    public async Task<string> GetUserRoleForTenantAsync(Guid userId, Guid tenantId)
    {
        var userTenant = await _context.UserTenants
            .FirstOrDefaultAsync(ut => 
                ut.UserId == userId && 
                ut.TenantId == tenantId && 
                ut.IsActive);
        
        return userTenant?.RoleCode ?? "user"; // Default to 'user' role
    }

    public async Task<List<string>> GetUserPermissionsAsync(Guid userId, Guid tenantId)
    {
        var roleCode = await GetUserRoleForTenantAsync(userId, tenantId);
        var role = await _context.Roles
            .FirstOrDefaultAsync(r => r.Name == roleCode);
        
        return role?.Permissions ?? new List<string>();
    }
}

// Register in Program.cs
builder.Services.AddScoped<UserService>();
```

---

## Role Seeding

```csharp
// Infrastructure/Data/RoleSeeder.cs
public static class RoleSeeder
{
    public static async Task SeedRolesAsync(RoleManager<ApplicationRole> roleManager)
    {
        // Admin Role - Full control within tenant
        if (!await roleManager.RoleExistsAsync("admin"))
        {
            var adminRole = new ApplicationRole
            {
                Name = "admin",
                NormalizedName = "ADMIN",
                Description = "Full control within tenant",
                Permissions = new List<string>
                {
                    "tenants:read",
                    "tenants:update",
                    "users:*",
                    "connectors:*",
                    "transformations:*",
                    "pipelines:*",
                    "executions:*",
                    "dashboard:*"
                }
            };
            await roleManager.CreateAsync(adminRole);
        }

        // Manager Role - Manage resources and users
        if (!await roleManager.RoleExistsAsync("manager"))
        {
            var managerRole = new ApplicationRole
            {
                Name = "manager",
                NormalizedName = "MANAGER",
                Description = "Manage resources and users",
                Permissions = new List<string>
                {
                    "tenants:read",
                    "users:read",
                    "users:create",
                    "users:update",
                    "connectors:*",
                    "transformations:*",
                    "pipelines:*",
                    "executions:read",
                    "executions:cancel",
                    "dashboard:*"
                }
            };
            await roleManager.CreateAsync(managerRole);
        }

        // User Role - Basic access
        if (!await roleManager.RoleExistsAsync("user"))
        {
            var userRole = new ApplicationRole
            {
                Name = "user",
                NormalizedName = "USER",
                Description = "Basic access - view and execute",
                Permissions = new List<string>
                {
                    "tenants:read",
                    "users:read:self",
                    "users:update:self",
                    "connectors:read",
                    "transformations:read",
                    "pipelines:read",
                    "pipelines:execute",
                    "executions:read",
                    "executions:cancel:own",
                    "dashboard:read"
                }
            };
            await roleManager.CreateAsync(userRole);
        }
    }
}

// Call in Program.cs after migrations
using (var scope = app.Services.CreateScope())
{
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
    await RoleSeeder.SeedRolesAsync(roleManager);
}
```

---

## Enhanced JWT Claims

Update `CreateClaimsPrincipalAsync` in AuthenticationController to include role and permissions:

```csharp
// In AuthenticationController (update the existing method)
private async Task<ClaimsPrincipal> CreateClaimsPrincipalAsync(
    ApplicationUser user, 
    ImmutableArray<string> scopes)
{
    var principal = await _signInManager.CreateUserPrincipalAsync(user);
    var identity = (ClaimsIdentity)principal.Identity;

    // Basic claims
    identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()));
    identity.AddClaim(new Claim(ClaimTypes.Email, user.Email));
    identity.AddClaim(new Claim(ClaimTypes.Name, $"{user.FirstName} {user.LastName}"));
    identity.AddClaim(new Claim("tenant_id", user.CurrentTenantId?.ToString() ?? ""));

    // Get user's role for current tenant
    if (user.CurrentTenantId.HasValue)
    {
        var userTenant = await _context.UserTenants
            .Include(ut => ut.Tenant)
            .FirstOrDefaultAsync(ut => 
                ut.UserId == user.Id && 
                ut.TenantId == user.CurrentTenantId.Value &&
                ut.IsActive);

        if (userTenant != null)
        {
            // Add role claim
            identity.AddClaim(new Claim(ClaimTypes.Role, userTenant.RoleCode));
            
            // Add tenant name claim
            identity.AddClaim(new Claim("tenant_name", userTenant.Tenant.Name));

            // Get role permissions
            var role = await _roleManager.FindByNameAsync(userTenant.RoleCode);
            if (role != null)
            {
                // Add permissions as individual claims
                foreach (var permission in role.Permissions)
                {
                    identity.AddClaim(new Claim("permission", permission));
                }

                // Also add permissions as a single JSON claim for easy access
                identity.AddClaim(new Claim("permissions", 
                    JsonSerializer.Serialize(role.Permissions)));
            }
        }
    }

    // Set claim destinations (which claims go in which tokens)
    identity.SetDestinations(claim => claim.Type switch
    {
        // Never include security stamp
        "AspNet.Identity.SecurityStamp" => ImmutableArray<string>.Empty,

        // ID token + access token
        ClaimTypes.NameIdentifier 
        or ClaimTypes.Name 
        or ClaimTypes.Email 
        or ClaimTypes.Role
        or "tenant_id" 
        or "tenant_name"
            => ImmutableArray.Create(
                OpenIddictConstants.Destinations.AccessToken,
                OpenIddictConstants.Destinations.IdentityToken),

        // Access token only
        "permission" or "permissions"
            => ImmutableArray.Create(OpenIddictConstants.Destinations.AccessToken),

        // Default: access token only
        _ => ImmutableArray.Create(OpenIddictConstants.Destinations.AccessToken)
    });

    return principal;
}
```

---

## Current User Service

Service to easily access current user's claims:

```csharp
// Application/Common/Interfaces/ICurrentUserService.cs
public interface ICurrentUserService
{
    Guid GetUserId();
    Guid GetTenantId();
    string GetRole();
    List<string> GetPermissions();
    bool HasPermission(string permission);
    bool IsInRole(string role);
}

// Infrastructure/Identity/CurrentUserService.cs
using System.Security.Claims;
using System.Text.Json;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private ClaimsPrincipal User => _httpContextAccessor.HttpContext?.User;

    public Guid GetUserId()
    {
        var userIdClaim = User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userIdClaim, out var userId) ? userId : Guid.Empty;
    }

    public Guid GetTenantId()
    {
        var tenantIdClaim = User?.FindFirst("tenant_id")?.Value;
        return Guid.TryParse(tenantIdClaim, out var tenantId) ? tenantId : Guid.Empty;
    }

    public string GetRole()
    {
        return User?.FindFirst(ClaimTypes.Role)?.Value ?? "user";
    }

    public List<string> GetPermissions()
    {
        // Get from JSON claim
        var permissionsJson = User?.FindFirst("permissions")?.Value;
        if (!string.IsNullOrEmpty(permissionsJson))
        {
            return JsonSerializer.Deserialize<List<string>>(permissionsJson) ?? new List<string>();
        }

        // Fallback: get individual permission claims
        return User?.FindAll("permission").Select(c => c.Value).ToList() ?? new List<string>();
    }

    public bool HasPermission(string permission)
    {
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

            // Check {resource}:*
            if (permissions.Contains($"{resource}:*"))
                return true;

            // Check *:{action}
            if (permissions.Contains($"*:{action}"))
                return true;

            // Check super admin
            if (permissions.Contains("*:*"))
                return true;
        }

        return false;
    }

    public bool IsInRole(string role)
    {
        return User?.IsInRole(role) ?? false;
    }
}

// Register in Program.cs
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
```

---

## Using in Controllers

```csharp
[ApiController]
[Route("api/[controller]")]
public class PipelinesController : ControllerBase
{
    private readonly ICurrentUserService _currentUser;
    private readonly ApplicationDbContext _context;

    public PipelinesController(ICurrentUserService currentUser, ApplicationDbContext context)
    {
        _currentUser = currentUser;
        _context = context;
    }

    [HttpGet]
    [Authorize] // Just require authentication
    public async Task<IActionResult> GetPipelines()
    {
        var tenantId = _currentUser.GetTenantId();
        
        // Automatically filtered by tenant
        var pipelines = await _context.Pipelines
            .Where(p => p.TenantId == tenantId)
            .ToListAsync();

        return Ok(pipelines);
    }

    [HttpPost]
    [Authorize(Roles = "admin,manager")] // Require specific roles
    public async Task<IActionResult> CreatePipeline([FromBody] PipelineDto dto)
    {
        // Additional permission check
        if (!_currentUser.HasPermission("pipelines:create"))
        {
            return Forbid();
        }

        var tenantId = _currentUser.GetTenantId();
        var userId = _currentUser.GetUserId();

        var pipeline = new Pipeline
        {
            TenantId = tenantId,
            CreatedBy = userId,
            Name = dto.Name,
            // ... other properties
        };

        _context.Pipelines.Add(pipeline);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetPipeline), new { id = pipeline.Id }, pipeline);
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "admin")] // Admin only
    public async Task<IActionResult> DeletePipeline(Guid id)
    {
        var tenantId = _currentUser.GetTenantId();
        
        var pipeline = await _context.Pipelines
            .FirstOrDefaultAsync(p => p.Id == id && p.TenantId == tenantId);

        if (pipeline == null)
            return NotFound();

        _context.Pipelines.Remove(pipeline);
        await _context.SaveChangesAsync();

        return NoContent();
    }
}
```

---

## JWT Token Example

After successful authentication, the JWT will contain:

```json
{
  "sub": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "email": "john@acmecorp.com",
  "name": "John Doe",
  "role": "admin",
  "tenant_id": "8fa85f64-5717-4562-b3fc-2c963f66afa7",
  "tenant_name": "Acme Corp",
  "permissions": "[\"tenants:read\",\"tenants:update\",\"users:*\",\"connectors:*\",\"transformations:*\",\"pipelines:*\",\"executions:*\",\"dashboard:*\"]",
  "permission": [
    "tenants:read",
    "tenants:update",
    "users:*",
    "connectors:*",
    "transformations:*",
    "pipelines:*",
    "executions:*",
    "dashboard:*"
  ],
  "exp": 1700500000,
  "aud": "multitenant-etl-spa",
  "iss": "https://api.yourapp.com"
}
```

---

## Frontend Usage

```typescript
// Decode JWT on frontend
import jwtDecode from 'jwt-decode';

interface JwtPayload {
  sub: string;
  email: string;
  name: string;
  role: string;
  tenant_id: string;
  tenant_name: string;
  permissions: string; // JSON string
}

const token = localStorage.getItem('access_token');
const decoded = jwtDecode<JwtPayload>(token);

console.log(decoded.role); // "admin"
console.log(decoded.tenant_name); // "Acme Corp"

// Parse permissions
const permissions = JSON.parse(decoded.permissions);

// Check permissions in UI
function canCreatePipeline(): boolean {
  return permissions.includes('pipelines:create') || 
         permissions.includes('pipelines:*') ||
         permissions.includes('*:*');
}

// Show/hide UI elements based on role
const isAdmin = decoded.role === 'admin';
const isManager = decoded.role === 'manager' || isAdmin;
```

---

## Checklist

- [ ] UserService created and registered
- [ ] Roles seeded (admin, manager, user)
- [ ] CreateClaimsPrincipalAsync updated with role/permission claims
- [ ] CurrentUserService created and registered
- [ ] Controllers use `[Authorize(Roles = "...")]`
- [ ] Permission checking with `HasPermission()` tested
- [ ] JWT contains role and permissions
- [ ] Frontend can decode and use JWT claims

---

**Next:** [5. Security Features](./5-security.md)
