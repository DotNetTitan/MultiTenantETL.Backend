# Authorization & Permissions Matrix

**For:** ASP.NET Core Web API  
**Version:** 1.0  
**Last Updated:** 2025-11-19

---

## Overview

This document defines the complete authorization model including roles, permissions, and access control rules.

---

## Role Hierarchy

```
Super Admin (Platform)
    └─ Tenant Admin
        └─ Manager
            └─ User
```

**Note:** Only Tenant Admin, Manager, and User roles exist in the database. Super Admin is a special role for platform administrators.

---

## Permissions Matrix

### Core Resources

| Resource | Action | Admin | Manager | User | Notes |
|----------|--------|-------|---------|------|-------|
| **Tenants** |
| | List | ✅ | ✅ | ✅ | Users see only their tenant |
| | View | ✅ | ✅ | ✅ | Own tenant only |
| | Create | ❌ | ❌ | ❌ | Platform admin only |
| | Update | ✅ | ❌ | ❌ | Own tenant only |
| | Delete | ❌ | ❌ | ❌ | Platform admin only |
| | Toggle Status | ✅ | ❌ | ❌ | Own tenant only |
| **Users** |
| | List | ✅ | ✅ | ❌ | Own tenant only |
| | View | ✅ | ✅ | ✅ | Self or same tenant |
| | Create | ✅ | ✅ | ❌ | Own tenant only |
| | Update | ✅ | ✅ | ✅ | Self or subordinates |
| | Delete | ✅ | ✅ | ❌ | Not self, same tenant |
| | Change Role | ✅ | ❌ | ❌ | Cannot promote to Admin |
| | Reset Password | ✅ | ✅ | ✅ | Self only for non-admins |
| **Connectors** |
| | List | ✅ | ✅ | ✅ | Own tenant only |
| | View | ✅ | ✅ | ✅ | Own tenant only |
| | Create | ✅ | ✅ | ❌ | Own tenant only |
| | Update | ✅ | ✅ | ❌ | Own tenant only |
| | Delete | ✅ | ✅ | ❌ | Check pipeline usage |
| | Test Connection | ✅ | ✅ | ✅ | Own tenant only |
| | Get Schema | ✅ | ✅ | ✅ | Own tenant only |
| **Transformations** |
| | List | ✅ | ✅ | ✅ | Own tenant only |
| | View | ✅ | ✅ | ✅ | Own tenant only |
| | Create | ✅ | ✅ | ❌ | Own tenant only |
| | Update | ✅ | ✅ | ❌ | Check pipeline usage |
| | Delete | ✅ | ✅ | ❌ | Check pipeline usage |
| | Clone | ✅ | ✅ | ✅ | Own tenant only |
| **Pipelines** |
| | List | ✅ | ✅ | ✅ | Own tenant only |
| | View | ✅ | ✅ | ✅ | Own tenant only |
| | Create | ✅ | ✅ | ❌ | Own tenant only |
| | Update | ✅ | ✅ | ❌ | Not while running |
| | Delete | ✅ | ✅ | ❌ | Not while running |
| | Execute | ✅ | ✅ | ✅ | Own tenant only |
| | Cancel Execution | ✅ | ✅ | ✅ | Own tenant only |
| | View Schedule | ✅ | ✅ | ✅ | Own tenant only |
| | Update Schedule | ✅ | ✅ | ❌ | Own tenant only |
| **Executions** |
| | List | ✅ | ✅ | ✅ | Own tenant only |
| | View | ✅ | ✅ | ✅ | Own tenant only |
| | View Logs | ✅ | ✅ | ✅ | Own tenant only |
| | Delete | ✅ | ❌ | ❌ | Historical cleanup |
| | Cancel | ✅ | ✅ | ✅ | Own executions |
| **Dashboard** |
| | View Stats | ✅ | ✅ | ✅ | Tenant-scoped data |
| | Export Data | ✅ | ✅ | ❌ | Own tenant only |

---

## Permission Codes

### Format
```
{resource}:{action}
```

### Examples
```
tenants:read
tenants:write
users:create
users:delete
connectors:execute
pipelines:manage
executions:view
dashboard:access
```

### Wildcard Permissions
```
*:*              - Super admin (all permissions)
pipelines:*      - All pipeline operations
*:read           - Read access to all resources
```

---

## Role Definitions

### Admin Role

```json
{
  "code": "admin",
  "displayName": "Admin",
  "permissions": [
    "tenants:read",
    "tenants:update",
    "users:*",
    "connectors:*",
    "transformations:*",
    "pipelines:*",
    "executions:*",
    "dashboard:*"
  ]
}
```

**Capabilities:**
- Full control within their tenant
- User management (create, update, delete users)
- Cannot create/delete tenants (platform admin only)
- Cannot promote users to Admin role

### Manager Role

```json
{
  "code": "manager",
  "displayName": "Manager",
  "permissions": [
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
  ]
}
```

**Capabilities:**
- Manage connectors, transformations, pipelines
- Create and manage users (but not delete)
- Execute and monitor pipelines
- View all execution logs
- Cannot change user roles

### User Role

```json
{
  "code": "user",
  "displayName": "User",
  "permissions": [
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
  ]
}
```

**Capabilities:**
- View connectors, transformations, pipelines
- Execute pipelines
- View own execution history
- Update own profile
- Read-only access to most resources

---

## Implementation

### Authorization Attributes

```csharp
// Require specific permission
[Authorize(Policy = "RequirePermission")]
[Permission("pipelines:create")]
public async Task<IActionResult> CreatePipeline([FromBody] CreatePipelineDto dto)
{
    // Implementation
}

// Require role
[Authorize(Roles = "admin,manager")]
public async Task<IActionResult> DeleteConnector(Guid id)
{
    // Implementation
}

// Multiple permissions (OR logic)
[Authorize(Policy = "RequireAnyPermission")]
[Permissions("pipelines:create", "pipelines:update")]
public async Task<IActionResult> SavePipeline([FromBody] PipelineDto dto)
{
    // Implementation
}
```

### Authorization Handlers

```csharp
// Permission-based authorization
public class PermissionRequirement : IAuthorizationRequirement
{
    public string Permission { get; }
    
    public PermissionRequirement(string permission)
    {
        Permission = permission;
    }
}

public class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    private readonly ICurrentUserService _currentUser;
    
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        var userPermissions = await _currentUser.GetPermissionsAsync();
        
        // Check for exact permission
        if (userPermissions.Contains(requirement.Permission))
        {
            context.Succeed(requirement);
            return;
        }
        
        // Check for wildcard permissions
        var parts = requirement.Permission.Split(':');
        if (parts.Length == 2)
        {
            var resource = parts[0];
            var action = parts[1];
            
            // Check {resource}:*
            if (userPermissions.Contains($"{resource}:*"))
            {
                context.Succeed(requirement);
                return;
            }
            
            // Check *:{action}
            if (userPermissions.Contains($"*:{action}"))
            {
                context.Succeed(requirement);
                return;
            }
        }
        
        // Check for super admin
        if (userPermissions.Contains("*:*"))
        {
            context.Succeed(requirement);
        }
    }
}

// Register in Startup.cs
services.AddAuthorization(options =>
{
    options.AddPolicy("RequirePermission", policy =>
        policy.Requirements.Add(new PermissionRequirement(string.Empty)));
});

services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();
```

### Resource-Based Authorization

```csharp
// For checking ownership or tenant isolation
public class TenantResourceRequirement : IAuthorizationRequirement { }

public class TenantResourceAuthorizationHandler : 
    AuthorizationHandler<TenantResourceRequirement, ITenantResource>
{
    private readonly ICurrentUserService _currentUser;
    
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        TenantResourceRequirement requirement,
        ITenantResource resource)
    {
        var userTenantId = _currentUser.GetTenantId();
        
        if (resource.TenantId == userTenantId)
        {
            context.Succeed(requirement);
        }
        
        return Task.CompletedTask;
    }
}

// Usage
var pipeline = await _db.Pipelines.FindAsync(id);
var authResult = await _authorizationService.AuthorizeAsync(
    User, 
    pipeline, 
    new TenantResourceRequirement()
);

if (!authResult.Succeeded)
{
    return Forbid();
}
```

---

## Tenant Isolation

### Database Level (Row-Level Security)

```sql
-- Set tenant context for each request
SET app.current_tenant_id = 'tenant-uuid';

-- RLS policies automatically filter data
SELECT * FROM pipelines;  -- Only returns current tenant's pipelines
```

### Application Level

```csharp
// Tenant filter middleware
public class TenantFilterMiddleware
{
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        var tenantId = context.User.FindFirst("tenant_id")?.Value;
        
        if (!string.IsNullOrEmpty(tenantId))
        {
            context.Items["TenantId"] = Guid.Parse(tenantId);
        }
        
        await next(context);
    }
}

// Query filter in DbContext
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    // Apply tenant filter to all tenant-scoped entities
    foreach (var entityType in modelBuilder.Model.GetEntityTypes())
    {
        if (typeof(ITenantEntity).IsAssignableFrom(entityType.ClrType))
        {
            var parameter = Expression.Parameter(entityType.ClrType, "e");
            var tenantId = _currentUser.GetTenantId();
            
            var filter = Expression.Lambda(
                Expression.Equal(
                    Expression.Property(parameter, nameof(ITenantEntity.TenantId)),
                    Expression.Constant(tenantId)
                ),
                parameter
            );
            
            modelBuilder.Entity(entityType.ClrType).HasQueryFilter(filter);
        }
    }
}
```

---

## Special Cases

### Self-Service Actions

Users can always:
- View their own profile
- Update their own profile (except role)
- Change their own password
- View their own execution history

```csharp
public async Task<IActionResult> UpdateUser(Guid id, UpdateUserDto dto)
{
    var currentUserId = _currentUser.GetUserId();
    
    // Allow updating self
    if (id == currentUserId)
    {
        // Cannot change own role
        if (dto.Role != null)
        {
            return Forbid("Cannot change your own role");
        }
        
        await _userService.UpdateAsync(id, dto);
        return Ok();
    }
    
    // Otherwise check permissions
    if (!await _currentUser.HasPermissionAsync("users:update"))
    {
        return Forbid();
    }
    
    // Managers cannot promote to Admin
    if (dto.Role == "admin" && _currentUser.GetRole() == "manager")
    {
        return Forbid("Managers cannot create admin users");
    }
    
    await _userService.UpdateAsync(id, dto);
    return Ok();
}
```

### Cross-Tenant Access

**Rule:** Users can NEVER access resources from other tenants.

```csharp
// Always validate tenant ownership
public async Task<IActionResult> GetPipeline(Guid id)
{
    var pipeline = await _db.Pipelines.FindAsync(id);
    
    if (pipeline == null)
    {
        return NotFound();
    }
    
    // Verify tenant ownership
    var userTenantId = _currentUser.GetTenantId();
    if (pipeline.TenantId != userTenantId)
    {
        // Return 404 instead of 403 to avoid data leakage
        return NotFound();
    }
    
    return Ok(pipeline);
}
```

---

## API Key Permissions

API keys have scoped permissions:

```json
{
  "keyId": "uuid",
  "tenantId": "uuid",
  "name": "External Integration",
  "scopes": [
    "connectors:read",
    "pipelines:execute",
    "executions:read"
  ]
}
```

**Validation:**
```csharp
public class ApiKeyScopeRequirement : IAuthorizationRequirement
{
    public string Scope { get; }
    
    public ApiKeyScopeRequirement(string scope)
    {
        Scope = scope;
    }
}

// Check API key scopes
var apiKeyId = context.User.FindFirst("api_key_id")?.Value;
if (!string.IsNullOrEmpty(apiKeyId))
{
    var apiKey = await _db.ApiKeys.FindAsync(Guid.Parse(apiKeyId));
    if (apiKey.Scopes.Contains(requirement.Scope))
    {
        context.Succeed(requirement);
    }
}
```

---

## Testing Authorization

```csharp
[Fact]
public async Task Manager_CannotDelete_Users()
{
    // Arrange
    var user = CreateManagerUser();
    var targetUser = CreateRegularUser();
    
    // Act
    var result = await _authorizationService.AuthorizeAsync(
        CreatePrincipal(user),
        targetUser,
        "users:delete"
    );
    
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
```
