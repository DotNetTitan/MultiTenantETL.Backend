# Tenant Management Guide

Complete guide for managing tenants in the MultiTenant ETL platform.

## Overview

The platform supports multi-tenancy where:
- Each user can belong to multiple tenants
- Each user has a role within each tenant (TenantAdmin, Manager, User)
- Users can switch between their tenants
- Data is isolated per tenant
- New users automatically get a personal workspace tenant

## Architecture

### Models

**Tenant** (Domain Entity)
- `Id` - Unique identifier
- `Name` - Display name
- `Slug` - URL-friendly identifier (unique)
- `IsActive` - Soft delete flag
- `CreatedAt` - Creation timestamp

**UserTenant** (Infrastructure)
- `UserId` - User identifier
- `TenantId` - Tenant identifier
- `RoleCode` - User's role within the tenant
- `IsActive` - Whether the user is active in this tenant

### Services

**ITenantService** - Located in `Infrastructure/Interfaces/ITenantService.cs`

Provides methods for:
- Creating, reading, updating, and deleting tenants
- Managing user-tenant relationships
- Switching active tenant
- Listing tenants and users

## API Endpoints

### Tenant CRUD Operations

#### Get All Tenants (SuperAdmin Only)
```http
GET /api/tenants
Authorization: Bearer {token}
```

Response:
```json
[
  {
    "id": "guid",
    "name": "Acme Corp",
    "slug": "acme-corp",
    "isActive": true,
    "createdAt": "2024-01-01T00:00:00Z"
  }
]
```

#### Get My Tenants
```http
GET /api/tenants/my-tenants
Authorization: Bearer {token}
```

Response:
```json
[
  {
    "tenantId": "guid",
    "tenantName": "Acme Corp",
    "tenantSlug": "acme-corp",
    "roleCode": "tenant_admin",
    "isActive": true,
    "isCurrent": true
  }
]
```

#### Get Tenant by ID
```http
GET /api/tenants/{id}
Authorization: Bearer {token}
```

Response:
```json
{
  "id": "guid",
  "name": "Acme Corp",
  "slug": "acme-corp",
  "isActive": true,
  "createdAt": "2024-01-01T00:00:00Z"
}
```

#### Create Tenant (SuperAdmin Only)
```http
POST /api/tenants
Authorization: Bearer {token}
Content-Type: application/json

{
  "name": "Acme Corp",
  "slug": "acme-corp"
}
```

Validation:
- Name: 2-100 characters
- Slug: 2-50 characters, lowercase letters, numbers, and hyphens only
- Slug must be unique

Response:
```json
{
  "id": "guid",
  "name": "Acme Corp",
  "slug": "acme-corp",
  "isActive": true,
  "createdAt": "2024-01-01T00:00:00Z"
}
```

#### Update Tenant (SuperAdmin or TenantAdmin)
```http
PUT /api/tenants/{id}
Authorization: Bearer {token}
Content-Type: application/json

{
  "name": "Acme Corporation",
  "isActive": true
}
```

Note: TenantAdmin can only update their own tenant.

#### Delete Tenant (SuperAdmin Only)
```http
DELETE /api/tenants/{id}
Authorization: Bearer {token}
```

Note: This is a soft delete (sets `IsActive = false`).

### User-Tenant Management

#### Get Tenant Users (SuperAdmin or TenantAdmin)
```http
GET /api/tenants/{id}/users
Authorization: Bearer {token}
```

Response:
```json
[
  {
    "userId": "guid",
    "email": "user@example.com",
    "firstName": "John",
    "lastName": "Doe",
    "roleCode": "manager",
    "isActive": true
  }
]
```

#### Add User to Tenant (SuperAdmin or TenantAdmin)
```http
POST /api/tenants/{id}/users
Authorization: Bearer {token}
Content-Type: application/json

{
  "userId": "guid",
  "tenantId": "guid",
  "roleCode": "manager"
}
```

Note: TenantAdmin can only add users to their own tenant.

Response:
```json
{
  "userId": "guid",
  "tenantId": "guid",
  "roleCode": "manager",
  "message": "User added to tenant successfully"
}
```

#### Remove User from Tenant (SuperAdmin or TenantAdmin)
```http
DELETE /api/tenants/{tenantId}/users/{userId}
Authorization: Bearer {token}
```

Note: TenantAdmin can only remove users from their own tenant.

#### Update User Role in Tenant (SuperAdmin or TenantAdmin)
```http
PUT /api/tenants/{tenantId}/users/{userId}/role
Authorization: Bearer {token}
Content-Type: application/json

{
  "roleCode": "tenant_admin"
}
```

Note: TenantAdmin can only update roles in their own tenant.

### Tenant Switching

#### Switch Active Tenant
```http
POST /api/account/switch-tenant
Authorization: Bearer {token}
Content-Type: application/json

{
  "tenantId": "guid"
}
```

Response:
```json
{
  "currentTenantId": "guid",
  "tenantName": "Acme Corp",
  "message": "Tenant switched successfully. Use your current refresh token to get a new access token with updated tenant."
}
```

After switching, use the refresh token to get a new access token with updated tenant claims.

## Authorization

### Roles

- **SuperAdmin** - Full access to all tenants and operations
- **TenantAdmin** - Manage their own tenant and its users
- **Manager** - Manage resources within their tenant
- **User** - View and use resources within their tenant

### Permissions by Role

| Operation | SuperAdmin | TenantAdmin | Manager | User |
|-----------|------------|-------------|---------|------|
| Create tenant | ✅ | ❌ | ❌ | ❌ |
| View all tenants | ✅ | ❌ | ❌ | ❌ |
| View own tenants | ✅ | ✅ | ✅ | ✅ |
| Update any tenant | ✅ | ❌ | ❌ | ❌ |
| Update own tenant | ✅ | ✅ | ❌ | ❌ |
| Delete tenant | ✅ | ❌ | ❌ | ❌ |
| View tenant users | ✅ | ✅ (own) | ❌ | ❌ |
| Add user to tenant | ✅ | ✅ (own) | ❌ | ❌ |
| Remove user from tenant | ✅ | ✅ (own) | ❌ | ❌ |
| Update user role | ✅ | ✅ (own) | ❌ | ❌ |
| Switch tenant | ✅ | ✅ | ✅ | ✅ |

## Automatic Tenant Creation

When a new user registers, the system automatically:
1. Creates a personal workspace tenant with slug `user-{first-8-chars-of-user-id}`
2. Names it `{FirstName}'s Workspace`
3. Adds the user to this tenant with `TenantAdmin` role
4. Sets it as their current tenant

Example:
- User: John Doe (ID: `a1b2c3d4-e5f6-7890-abcd-ef1234567890`)
- Tenant Name: `John's Workspace`
- Tenant Slug: `user-a1b2c3d4`

## Common Workflows

### Creating a New Organization Tenant

1. SuperAdmin creates tenant:
```bash
curl -X POST https://api.example.com/api/tenants \
  -H "Authorization: Bearer {superadmin-token}" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "Acme Corp",
    "slug": "acme-corp"
  }'
```

2. SuperAdmin adds users to tenant:
```bash
curl -X POST https://api.example.com/api/tenants/{tenant-id}/users \
  -H "Authorization: Bearer {superadmin-token}" \
  -H "Content-Type: application/json" \
  -d '{
    "userId": "{user-id}",
    "tenantId": "{tenant-id}",
    "roleCode": "tenant_admin"
  }'
```

### User Switching Between Tenants

1. Get list of available tenants:
```bash
curl -X GET https://api.example.com/api/tenants/my-tenants \
  -H "Authorization: Bearer {token}"
```

2. Switch to desired tenant:
```bash
curl -X POST https://api.example.com/api/account/switch-tenant \
  -H "Authorization: Bearer {token}" \
  -H "Content-Type: application/json" \
  -d '{
    "tenantId": "{tenant-id}"
  }'
```

3. Refresh access token:
```bash
curl -X POST https://api.example.com/connect/token \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "grant_type=refresh_token&client_id={client-id}&client_secret={secret}&refresh_token={refresh-token}"
```

### TenantAdmin Managing Users

1. View users in tenant:
```bash
curl -X GET https://api.example.com/api/tenants/{tenant-id}/users \
  -H "Authorization: Bearer {tenant-admin-token}"
```

2. Add new user:
```bash
curl -X POST https://api.example.com/api/tenants/{tenant-id}/users \
  -H "Authorization: Bearer {tenant-admin-token}" \
  -H "Content-Type: application/json" \
  -d '{
    "userId": "{user-id}",
    "tenantId": "{tenant-id}",
    "roleCode": "manager"
  }'
```

3. Update user role:
```bash
curl -X PUT https://api.example.com/api/tenants/{tenant-id}/users/{user-id}/role \
  -H "Authorization: Bearer {tenant-admin-token}" \
  -H "Content-Type: application/json" \
  -d '{
    "roleCode": "user"
  }'
```

## Error Codes

| Code | Description |
|------|-------------|
| `TenantNotFound` | Tenant does not exist |
| `TenantAlreadyExists` | Tenant with this slug already exists |
| `TenantAccessDenied` | User does not have access to this tenant |
| `UserAlreadyInTenant` | User is already a member of this tenant |
| `UserNotInTenant` | User is not a member of this tenant |
| `UserNotFound` | User does not exist |
| `ValidationError` | Request validation failed |

## Database Schema

### Tenants Table
```sql
CREATE TABLE "Tenants" (
    "Id" uuid PRIMARY KEY,
    "Name" varchar(100) NOT NULL,
    "Slug" varchar(50) NOT NULL UNIQUE,
    "IsActive" boolean NOT NULL,
    "CreatedAt" timestamp NOT NULL
);
```

### UserTenants Table
```sql
CREATE TABLE "UserTenants" (
    "UserId" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "RoleCode" varchar(50) NOT NULL,
    "IsActive" boolean NOT NULL,
    PRIMARY KEY ("UserId", "TenantId"),
    FOREIGN KEY ("UserId") REFERENCES "users"("Id"),
    FOREIGN KEY ("TenantId") REFERENCES "Tenants"("Id")
);
```

## Best Practices

1. **Slug Naming**: Use descriptive, URL-friendly slugs (e.g., `acme-corp`, `engineering-team`)
2. **Role Assignment**: Start users with minimal permissions and escalate as needed
3. **Tenant Switching**: Always refresh the access token after switching tenants
4. **Soft Deletes**: Tenants are soft-deleted to preserve data integrity
5. **Personal Workspaces**: Each user gets a personal workspace for individual work

## Security Considerations

- TenantAdmin can only manage their own tenant
- Users can only see tenants they belong to
- Tenant switching validates user membership
- All tenant operations are logged
- Soft deletes prevent accidental data loss

## Future Enhancements

Potential features to consider:
- Tenant invitations via email
- Tenant transfer (change ownership)
- Tenant usage quotas and limits
- Tenant-level settings and customization
- Audit logs for tenant operations
- Bulk user import/export
