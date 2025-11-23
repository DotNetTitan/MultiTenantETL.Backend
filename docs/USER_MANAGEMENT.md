# User Management Guide

Complete guide for managing users in the MultiTenant ETL platform.

## Overview

The user management system provides comprehensive CRUD operations for users with proper authorization controls. Users can manage their own profiles, while administrators can manage all users in the system.

## Features

- User profile management (self-service)
- User search and listing with filters
- User activation/deactivation
- Role assignment and management
- Admin password reset
- Pagination support
- Tenant-scoped user management

## API Endpoints

### Profile Management (Self-Service)

#### Get Current User Profile
```http
GET /api/users/me
Authorization: Bearer {token}
```

Response:
```json
{
  "id": "guid",
  "email": "user@example.com",
  "firstName": "John",
  "lastName": "Doe",
  "isActive": true,
  "emailConfirmed": true,
  "createdAt": "2024-01-01T00:00:00Z",
  "currentTenantId": "guid",
  "currentTenantName": "Acme Corp",
  "tenants": [
    {
      "tenantId": "guid",
      "tenantName": "Acme Corp",
      "roleCode": "tenant_admin",
      "isActive": true
    }
  ],
  "roles": ["tenant_admin"]
}
```

#### Update Current User Profile
```http
PUT /api/users/me
Authorization: Bearer {token}
Content-Type: application/json

{
  "firstName": "John",
  "lastName": "Doe",
  "email": "john.doe@example.com"
}
```

Response:
```json
{
  "id": "guid",
  "email": "john.doe@example.com",
  "firstName": "John",
  "lastName": "Doe",
  "isActive": true,
  "emailConfirmed": true,
  "createdAt": "2024-01-01T00:00:00Z",
  "currentTenantId": "guid",
  "currentTenantName": "Acme Corp"
}
```

### User Management (Admin Operations)

#### Search/List Users (SuperAdmin or TenantAdmin)
```http
GET /api/users?email={email}&name={name}&isActive={true|false}&tenantId={guid}&page={1}&pageSize={20}
Authorization: Bearer {token}
```

Query Parameters:
- `email` (optional) - Filter by email (partial match)
- `name` (optional) - Filter by first or last name (partial match)
- `isActive` (optional) - Filter by active status
- `tenantId` (optional) - Filter by tenant membership (SuperAdmin only)
- `page` (optional, default: 1) - Page number
- `pageSize` (optional, default: 20) - Items per page

Response:
```json
{
  "users": [
    {
      "id": "guid",
      "email": "user@example.com",
      "firstName": "John",
      "lastName": "Doe",
      "isActive": true,
      "emailConfirmed": true,
      "createdAt": "2024-01-01T00:00:00Z",
      "currentTenantId": "guid",
      "currentTenantName": "Acme Corp"
    }
  ],
  "totalCount": 50,
  "page": 1,
  "pageSize": 20,
  "totalPages": 3
}
```

Note: TenantAdmin can only see users in their own tenant.

#### Get User by ID (SuperAdmin or TenantAdmin)
```http
GET /api/users/{id}
Authorization: Bearer {token}
```

Response:
```json
{
  "id": "guid",
  "email": "user@example.com",
  "firstName": "John",
  "lastName": "Doe",
  "isActive": true,
  "emailConfirmed": true,
  "createdAt": "2024-01-01T00:00:00Z",
  "currentTenantId": "guid",
  "currentTenantName": "Acme Corp",
  "tenants": [
    {
      "tenantId": "guid",
      "tenantName": "Acme Corp",
      "roleCode": "manager",
      "isActive": true
    }
  ],
  "roles": ["manager"]
}
```

Note: TenantAdmin can only view users in their own tenant.

#### Update User (SuperAdmin Only)
```http
PUT /api/users/{id}
Authorization: Bearer {token}
Content-Type: application/json

{
  "firstName": "Jane",
  "lastName": "Smith",
  "email": "jane.smith@example.com"
}
```

Response:
```json
{
  "id": "guid",
  "email": "jane.smith@example.com",
  "firstName": "Jane",
  "lastName": "Smith",
  "isActive": true,
  "emailConfirmed": true,
  "createdAt": "2024-01-01T00:00:00Z",
  "currentTenantId": "guid",
  "currentTenantName": "Acme Corp"
}
```

#### Update User Status (SuperAdmin Only)
```http
PUT /api/users/{id}/status
Authorization: Bearer {token}
Content-Type: application/json

{
  "isActive": false
}
```

Response:
```json
{
  "message": "User deactivated successfully"
}
```

#### Delete User (SuperAdmin Only)
```http
DELETE /api/users/{id}
Authorization: Bearer {token}
```

Note: This is a soft delete (sets `IsActive = false`).

Response: `204 No Content`

### Role Management

#### Assign Role to User (SuperAdmin Only)
```http
POST /api/users/{id}/roles
Authorization: Bearer {token}
Content-Type: application/json

{
  "roleName": "tenant_admin"
}
```

Available roles:
- `super_admin` - Full system access
- `tenant_admin` - Tenant administration
- `manager` - Resource management
- `user` - Basic access

Response:
```json
{
  "message": "Role 'tenant_admin' assigned successfully"
}
```

#### Remove Role from User (SuperAdmin Only)
```http
DELETE /api/users/{id}/roles
Authorization: Bearer {token}
Content-Type: application/json

{
  "roleName": "manager"
}
```

Response:
```json
{
  "message": "Role 'manager' removed successfully"
}
```

### Password Management

#### Admin Reset Password (SuperAdmin Only)
```http
POST /api/users/{id}/reset-password
Authorization: Bearer {token}
Content-Type: application/json

{
  "newPassword": "NewSecurePassword123!"
}
```

Password requirements:
- Minimum 8 characters
- At least one uppercase letter
- At least one lowercase letter
- At least one digit
- At least one special character

Response:
```json
{
  "message": "Password reset successfully"
}
```

Note: This is an admin operation. Users can reset their own passwords via the `/api/account/forgot-password` endpoint.

## Authorization Matrix

| Operation | SuperAdmin | TenantAdmin | Manager | User |
|-----------|------------|-------------|---------|------|
| View own profile | ✅ | ✅ | ✅ | ✅ |
| Update own profile | ✅ | ✅ | ✅ | ✅ |
| List all users | ✅ | ❌ | ❌ | ❌ |
| List tenant users | ✅ | ✅ (own) | ❌ | ❌ |
| View any user | ✅ | ✅ (own tenant) | ❌ | ❌ |
| Update any user | ✅ | ❌ | ❌ | ❌ |
| Activate/deactivate user | ✅ | ❌ | ❌ | ❌ |
| Delete user | ✅ | ❌ | ❌ | ❌ |
| Assign roles | ✅ | ❌ | ❌ | ❌ |
| Remove roles | ✅ | ❌ | ❌ | ❌ |
| Admin password reset | ✅ | ❌ | ❌ | ❌ |

## Common Workflows

### User Updates Their Profile

```bash
# Get current profile
curl -X GET https://api.example.com/api/users/me \
  -H "Authorization: Bearer {token}"

# Update profile
curl -X PUT https://api.example.com/api/users/me \
  -H "Authorization: Bearer {token}" \
  -H "Content-Type: application/json" \
  -d '{
    "firstName": "John",
    "lastName": "Doe",
    "email": "john.doe@example.com"
  }'
```

### Admin Searches for Users

```bash
# Search by email
curl -X GET "https://api.example.com/api/users?email=john" \
  -H "Authorization: Bearer {admin-token}"

# Search by name
curl -X GET "https://api.example.com/api/users?name=doe" \
  -H "Authorization: Bearer {admin-token}"

# Filter by tenant
curl -X GET "https://api.example.com/api/users?tenantId={tenant-id}" \
  -H "Authorization: Bearer {admin-token}"

# Paginated results
curl -X GET "https://api.example.com/api/users?page=2&pageSize=10" \
  -H "Authorization: Bearer {admin-token}"
```

### TenantAdmin Views Their Tenant Users

```bash
# List users in tenant (automatically filtered)
curl -X GET https://api.example.com/api/users \
  -H "Authorization: Bearer {tenant-admin-token}"
```

### Admin Manages User

```bash
# View user details
curl -X GET https://api.example.com/api/users/{user-id} \
  -H "Authorization: Bearer {admin-token}"

# Update user
curl -X PUT https://api.example.com/api/users/{user-id} \
  -H "Authorization: Bearer {admin-token}" \
  -H "Content-Type: application/json" \
  -d '{
    "firstName": "Jane",
    "lastName": "Smith",
    "email": "jane.smith@example.com"
  }'

# Deactivate user
curl -X PUT https://api.example.com/api/users/{user-id}/status \
  -H "Authorization: Bearer {admin-token}" \
  -H "Content-Type: application/json" \
  -d '{"isActive": false}'

# Delete user
curl -X DELETE https://api.example.com/api/users/{user-id} \
  -H "Authorization: Bearer {admin-token}"
```

### Admin Manages Roles

```bash
# Assign role
curl -X POST https://api.example.com/api/users/{user-id}/roles \
  -H "Authorization: Bearer {admin-token}" \
  -H "Content-Type: application/json" \
  -d '{"roleName": "tenant_admin"}'

# Remove role
curl -X DELETE https://api.example.com/api/users/{user-id}/roles \
  -H "Authorization: Bearer {admin-token}" \
  -H "Content-Type: application/json" \
  -d '{"roleName": "manager"}'
```

### Admin Resets User Password

```bash
curl -X POST https://api.example.com/api/users/{user-id}/reset-password \
  -H "Authorization: Bearer {admin-token}" \
  -H "Content-Type: application/json" \
  -d '{"newPassword": "NewSecurePassword123!"}'
```

## Search and Filtering

### Email Search
Partial match on email address:
```
GET /api/users?email=john
```
Matches: john@example.com, johnny@test.com, etc.

### Name Search
Partial match on first or last name:
```
GET /api/users?name=smith
```
Matches: John Smith, Jane Smithson, etc.

### Status Filter
Filter by active/inactive status:
```
GET /api/users?isActive=true
```

### Tenant Filter (SuperAdmin Only)
Filter by tenant membership:
```
GET /api/users?tenantId={guid}
```

### Combined Filters
```
GET /api/users?email=john&isActive=true&page=1&pageSize=10
```

## Pagination

All list endpoints support pagination:
- `page` - Page number (default: 1)
- `pageSize` - Items per page (default: 20, max: 100)

Response includes:
- `users` - Array of user objects
- `totalCount` - Total number of matching users
- `page` - Current page number
- `pageSize` - Items per page
- `totalPages` - Total number of pages

## Error Codes

| Code | Description |
|------|-------------|
| `UserNotFound` | User does not exist |
| `EmailAlreadyExists` | Email is already in use by another user |
| `ValidationError` | Request validation failed or operation error |

## Best Practices

1. **Profile Updates**: Users should update their own profiles via `/api/users/me`
2. **Email Changes**: Changing email requires re-confirmation (future enhancement)
3. **Soft Deletes**: Users are deactivated, not permanently deleted
4. **Role Management**: Use system roles (SuperAdmin, TenantAdmin) sparingly
5. **Password Resets**: Admin resets should be followed by user password change
6. **Search Performance**: Use specific filters to reduce result sets
7. **Pagination**: Always use pagination for large user lists

## Security Considerations

- Only SuperAdmin can modify other users' profiles
- TenantAdmin can only view users in their tenant
- Users can only view and update their own profile
- Email changes are validated for uniqueness
- Password resets require strong passwords
- All operations are logged for audit purposes
- Soft deletes preserve data integrity

## Integration with Tenant Management

Users can belong to multiple tenants with different roles:
- Use `/api/tenants/{id}/users` to manage tenant membership
- Use `/api/users/{id}` to view all tenant memberships
- User's `currentTenantId` determines their active context
- Switch tenants via `/api/account/switch-tenant`

## Future Enhancements

Potential features to consider:
- Email confirmation on email change
- User activity logs
- Bulk user operations
- User import/export
- Advanced search with multiple criteria
- User groups/teams
- Custom user fields
- Profile pictures
