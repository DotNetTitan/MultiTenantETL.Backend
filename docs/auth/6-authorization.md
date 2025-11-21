# 6. Authorization

**Purpose:** Permission-based authorization, authorization handlers, and tenant isolation

---

## Permission Matrix

### Roles
- **Admin** - Full control within tenant
- **Manager** - Manage resources and users
- **User** - View and execute

### Permission Format

```
{resource}:{action}
```

Examples:
- `pipelines:create`
- `users:delete`
- `connectors:*` (all connector actions)
- `*:read` (read all resources)
- `*:*` (super admin - all permissions)

### Complete Permissions by Role

**Admin Permissions:**
```
tenants:read, tenants:update
users:*, connectors:*, transformations:*, pipelines:*, executions:*, dashboard:*
```

**Manager Permissions:**
```
tenants:read
users:read, users:create, users:update
connectors:*, transformations:*, pipelines:*
executions:read, executions:cancel
dashboard:*
```

**User Permissions:**
```
tenants:read
users:read:self, users:update:self
connectors:read, transformations:read
pipelines:read, pipelines:execute
executions:read, executions:cancel:own
dashboard:read
```

---

## Authorization Handlers

### Permission Requirement

```csharp
// Application/Authorization/Requirements/PermissionRequirement.cs
using Microsoft.AspNetCore.Authorization;

public class PermissionRequirement : IAuthorizationRequirement
{
    public string Permission { get; }
    
    public PermissionRequirement(string permission)
    {
        Permission = permission;
    }
}
```

### Permission Authorization Handler

```csharp
// Application/Authorization/Handlers/PermissionAuthorizationHandler.cs
using Microsoft.AspNetCore.Authorization;

public class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    private readonly ICurrentUserService _currentUser;
    
    public PermissionAuthorizationHandler(ICurrentUserService currentUser)
    {
        _currentUser = currentUser;
    }
    
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        var userPermissions = _currentUser.GetPermissions();
        
        // Check for exact permission
        if (userPermissions.Contains(requirement.Permission))
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }
        
        // Check for wildcard permissions
        var parts = requirement.Permission.Split(':');
        if (parts.Length == 2)
        {
            var resource = parts[0];
            var action = parts[1];
            
            // Check {resource}:* (e.g., "pipelines:*")
            if (userPermissions.Contains($"{resource}:*"))
            {
                context.Succeed(requirement);
                return Task.CompletedTask;
            }
            
            // Check *:{action} (e.g., "*:read")
            if (userPermissions.Contains($"*:{action}"))
            {
                context.Succeed(requirement);
                return Task.CompletedTask;
            }
        }
        
        // Check for super admin (all permissions)
        if (userPermissions.Contains("*:*"))
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }
        
        // Permission not found - fail
        return Task.CompletedTask;
    }
}
```

### Tenant Resource Authorization

```csharp
// Application/Authorization/Requirements/TenantResourceRequirement.cs
public class TenantResourceRequirement : IAuthorizationRequirement { }

// Application/Authorization/Handlers/TenantResourceAuthorizationHandler.cs
public class TenantResourceAuthorizationHandler : 
    AuthorizationHandler<TenantResourceRequirement, ITenantResource>
{
    private readonly ICurrentUserService _currentUser;
    
    public TenantResourceAuthorizationHandler(ICurrentUserService currentUser)
    {
        _currentUser = currentUser;
    }
    
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        TenantResourceRequirement requirement,
        ITenantResource resource)
    {
        var userTenantId = _currentUser.GetTenantId();
        
        // User can only access resources in their current tenant
        if (resource.TenantId == userTenantId)
        {
            context.Succeed(requirement);
        }
        
        return Task.CompletedTask;
    }
}

// Domain/Interfaces/ITenantResource.cs
public interface ITenantResource
{
    Guid TenantId { get; }
}
```

---

## Authorization Configuration

```csharp
// API/Program.cs
using Microsoft.AspNetCore.Authorization;

var builder = WebApplication.CreateBuilder(args);

// Add authorization with policies
builder.Services.AddAuthorization(options =>
{
    // Permission-based policy
    options.AddPolicy("RequirePermission", policy => 
    {
        policy.Requirements.Add(new PermissionRequirement(string.Empty));
    });
    
    // Tenant resource policy
    options.AddPolicy("TenantResource", policy =>
    {
        policy.Requirements.Add(new TenantResourceRequirement());
    });
    
    // Role-based policies
    options.AddPolicy("RequireAdmin", policy =>
    {
        policy.RequireRole("admin");
    });
    
    options.AddPolicy("RequireManagerOrAdmin", policy =>
    {
        policy.RequireRole("admin", "manager");
    });
});

// Register authorization handlers
builder.Services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();
builder.Services.AddSingleton<IAuthorizationHandler, TenantResourceAuthorizationHandler>();

var app = builder.Build();
app.Run();
```

---

## Using Authorization in Controllers

### Role-Based Authorization

```csharp
[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    // Only admins and managers
    [HttpPost]
    [Authorize(Roles = "admin,manager")]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserDto dto)
    {
        // Implementation
    }
    
    // Admin only
    [HttpDelete("{id}")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> DeleteUser(Guid id)
    {
        // Implementation
    }
}
```

### Permission-Based Authorization

```csharp
[ApiController]
[Route("api/[controller]")]
public class PipelinesController : ControllerBase
{
    private readonly ICurrentUserService _currentUser;
    
    // Check permission programmatically
    [HttpPost]
    [Authorize]
    public async Task<IActionResult> CreatePipeline([FromBody] PipelineDto dto)
    {
        if (!_currentUser.HasPermission("pipelines:create"))
        {
            return Forbid();
        }
        
        // Implementation
    }
}
```

### Resource-Based Authorization

```csharp
[ApiController]
[Route("api/[controller]")]
public class PipelinesController : ControllerBase
{
    private readonly IAuthorizationService _authorizationService;
    private readonly ApplicationDbContext _context;
    
    [HttpPut("{id}")]
    [Authorize]
    public async Task<IActionResult> UpdatePipeline(Guid id, [FromBody] UpdatePipelineDto dto)
    {
        var pipeline = await _context.Pipelines.FindAsync(id);
        if (pipeline == null)
            return NotFound();
        
        // Check if user can access this tenant's resource
        var authResult = await _authorizationService.AuthorizeAsync(
            User, 
            pipeline, 
            "TenantResource");
        
        if (!authResult.Succeeded)
        {
            return NotFound(); // Return 404 instead of 403 to avoid data leakage
        }
        
        // Check permission
        if (!_currentUser.HasPermission("pipelines:update"))
        {
            return Forbid();
        }
        
        // Update pipeline
        pipeline.Name = dto.Name;
        await _context.SaveChangesAsync();
        
        return Ok(pipeline);
    }
}
```

---

## Tenant Isolation

### Application-Level Query Filter

```csharp
// Infrastructure/Persistence/ApplicationDbContext.cs
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    base.OnModelCreating(modelBuilder);
    
    // Apply tenant filter to all tenant-scoped entities
    foreach (var entityType in modelBuilder.Model.GetEntityTypes())
    {
        if (typeof(ITenantResource).IsAssignableFrom(entityType.ClrType))
        {
            var method = typeof(ApplicationDbContext)
                .GetMethod(nameof(SetTenantQueryFilter), BindingFlags.NonPublic | BindingFlags.Instance)
                .MakeGenericMethod(entityType.ClrType);
            
            method.Invoke(this, new[] { modelBuilder });
        }
    }
}

private void SetTenantQueryFilter<TEntity>(ModelBuilder modelBuilder) 
    where TEntity : class, ITenantResource
{
    modelBuilder.Entity<TEntity>().HasQueryFilter(e => 
        e.TenantId == _currentUser.GetTenantId());
}
```

### Manual Tenant Checking

```csharp
public async Task<IActionResult> GetPipeline(Guid id)
{
    var userTenantId = _currentUser.GetTenantId();
    
    var pipeline = await _context.Pipelines
        .FirstOrDefaultAsync(p => p.Id == id && p.TenantId == userTenantId);
    
    if (pipeline == null)
    {
        // Return 404 instead of 403 to prevent information disclosure
        return NotFound();
    }
    
    return Ok(pipeline);
}
```

---

## Special Cases

### Self-Service Operations

Users can always manage their own profile:

```csharp
[HttpPut("profile")]
[Authorize]
public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileDto dto)
{
    var userId = _currentUser.GetUserId();
    var user = await _userManager.FindByIdAsync(userId.ToString());
    
    // Users can update their own profile
    user.FirstName = dto.FirstName;
    user.LastName = dto.LastName;
    
    // Cannot change own role
    if (dto.Role != null)
    {
        return BadRequest(new ErrorResponse(
            AuthErrorCode.ValidationError,
            "Cannot change your own role"));
    }
    
    await _userManager.UpdateAsync(user);
    return Ok(user);
}
```

### Cross-Tenant Prevention

```csharp
public async Task<IActionResult> GetPipeline(Guid id)
{
    var pipeline = await _context.Pipelines.FindAsync(id);
    
    if (pipeline == null)
    {
        return NotFound();
    }
    
    // Verify tenant ownership
    var userTenantId = _currentUser.GetTenantId();
    if (pipeline.TenantId != userTenantId)
    {
        // Return 404 instead of 403 to avoid information disclosure
        // Attacker shouldn't know if resource exists in another tenant
        return NotFound();
    }
    
    return Ok(pipeline);
}
```

---

## Testing Authorization

```csharp
[Fact]
public async Task Manager_CannotDelete_Users()
{
    // Arrange
    var managerUser = CreateManagerUser();
    var targetUser = CreateRegularUser();
    
    // Act
    var result = await _authorizationService.AuthorizeAsync(
        CreatePrincipal(managerUser),
        targetUser,
        "users:delete");
    
    // Assert
    Assert.False(result.Succeeded);
}

[Fact]
public async Task User_CannotAccess_OtherTenant_Pipeline()
{
    // Arrange
    var user = CreateUser(tenantId: "tenant-1");
    var pipeline = CreatePipeline(tenantId: "tenant-2");
    
    // Act
    var result = await _pipelineService.GetPipelineAsync(pipeline.Id, user);
    
    // Assert
    Assert.Null(result); // Should return null for security
}

[Fact]
public async Task Admin_HasWildcardPermissions()
{
    // Arrange
    var admin = CreateAdminUser();
    
    // Act
    var canCreate = _currentUser.HasPermission("pipelines:create");
    var canDelete = _currentUser.HasPermission("pipelines:delete");
    
    // Assert
    Assert.True(canCreate);
    Assert.True(canDelete);
}
```

---

## Checklist

- [ ] PermissionRequirement and handler created
- [ ] TenantResourceRequirement and handler created
- [ ] Authorization policies configured
- [ ] Handlers registered in Program.cs
- [ ] Controllers use `[Authorize]` attributes
- [ ] Permission checking with `HasPermission()` implemented
- [ ] Tenant isolation enforced
- [ ] Cross-tenant access prevented
- [ ] Self-service operations allowed
- [ ] Authorization tests written
- [ ] 404 returned instead of 403 for tenant isolation

---

**Complete!** All authentication & authorization modules finished.

See [../authorization-matrix.md](../authorization-matrix.md) for the complete permissions matrix.
