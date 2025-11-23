# API Summary

Complete overview of all API endpoints in the MultiTenant ETL platform.

## Table of Contents

- [Authentication](#authentication)
- [Account Management](#account-management)
- [User Management](#user-management)
- [Tenant Management](#tenant-management)

## Authentication

OAuth 2.0 & OpenID Connect endpoints powered by OpenIddict.

### Token Endpoint
**POST** `/connect/token`

Grant types:
- `password` - Username/password authentication
- `refresh_token` - Refresh access token
- `authorization_code` - Authorization code flow (with PKCE)

### Authorization Endpoint
**GET/POST** `/connect/authorize`

OAuth authorization endpoint for SPAs using Authorization Code + PKCE flow.

### Token Revocation
**POST** `/connect/revoke`

Revoke access or refresh tokens.

---

## Account Management

Self-service account operations.

| Endpoint | Method | Auth | Description |
|----------|--------|------|-------------|
| `/api/account/register` | POST | ❌ | Register new user account |
| `/api/account/confirm-email` | POST | ❌ | Confirm email address |
| `/api/account/forgot-password` | POST | ❌ | Request password reset email |
| `/api/account/reset-password` | POST | ❌ | Reset password with token |
| `/api/account/change-password` | POST | ✅ | Change current password |
| `/api/account/logout` | POST | ✅ | Logout and revoke all tokens |
| `/api/account/switch-tenant` | POST | ✅ | Switch active tenant |

**Documentation**: See [Authentication Guide](./auth/authentication-guide.md)

---

## User Management

User profile and administration endpoints.

### Profile Management (Self-Service)

| Endpoint | Method | Auth | Description |
|----------|--------|------|-------------|
| `/api/users/me` | GET | ✅ All | Get current user profile |
| `/api/users/me` | PUT | ✅ All | Update current user profile |

### User Administration

| Endpoint | Method | Auth | Description |
|----------|--------|------|-------------|
| `/api/users` | GET | ✅ Admin | List/search users with filters |
| `/api/users/{id}` | GET | ✅ Admin | Get user details by ID |
| `/api/users/{id}` | PUT | ✅ SuperAdmin | Update user profile |
| `/api/users/{id}/status` | PUT | ✅ SuperAdmin | Activate/deactivate user |
| `/api/users/{id}` | DELETE | ✅ SuperAdmin | Delete user (soft delete) |

### Role Management

| Endpoint | Method | Auth | Description |
|----------|--------|------|-------------|
| `/api/users/{id}/roles` | POST | ✅ SuperAdmin | Assign role to user |
| `/api/users/{id}/roles` | DELETE | ✅ SuperAdmin | Remove role from user |

### Password Management

| Endpoint | Method | Auth | Description |
|----------|--------|------|-------------|
| `/api/users/{id}/reset-password` | POST | ✅ SuperAdmin | Admin-initiated password reset |

**Documentation**: See [User Management Guide](./USER_MANAGEMENT.md)

---

## Tenant Management

Multi-tenant organization and membership management.

### Tenant CRUD

| Endpoint | Method | Auth | Description |
|----------|--------|------|-------------|
| `/api/tenants` | GET | ✅ SuperAdmin | List all tenants |
| `/api/tenants/my-tenants` | GET | ✅ All | Get current user's tenants |
| `/api/tenants/{id}` | GET | ✅ Member | Get tenant details |
| `/api/tenants` | POST | ✅ SuperAdmin | Create new tenant |
| `/api/tenants/{id}` | PUT | ✅ Admin | Update tenant |
| `/api/tenants/{id}` | DELETE | ✅ SuperAdmin | Delete tenant (soft delete) |

### Tenant Membership

| Endpoint | Method | Auth | Description |
|----------|--------|------|-------------|
| `/api/tenants/{id}/users` | GET | ✅ Admin | List users in tenant |
| `/api/tenants/{id}/users` | POST | ✅ Admin | Add user to tenant |
| `/api/tenants/{tenantId}/users/{userId}` | DELETE | ✅ Admin | Remove user from tenant |
| `/api/tenants/{tenantId}/users/{userId}/role` | PUT | ✅ Admin | Update user's role in tenant |

**Documentation**: See [Tenant Management Guide](./TENANT_MANAGEMENT.md)

---

## Authorization Levels

### ❌ No Auth
Public endpoints accessible without authentication.

### ✅ All
Any authenticated user can access.

### ✅ Member
User must be a member of the tenant being accessed.

### ✅ Admin
- **SuperAdmin**: Full system access
- **TenantAdmin**: Access to own tenant only

### ✅ SuperAdmin
Only SuperAdmin role can access.

---

## Common Response Codes

| Code | Description |
|------|-------------|
| `200 OK` | Request successful |
| `201 Created` | Resource created successfully |
| `204 No Content` | Request successful, no content to return |
| `400 Bad Request` | Invalid request or validation error |
| `401 Unauthorized` | Authentication required or invalid token |
| `403 Forbidden` | Insufficient permissions |
| `404 Not Found` | Resource not found |
| `429 Too Many Requests` | Rate limit exceeded |
| `500 Internal Server Error` | Server error |

---

## Error Response Format

All error responses follow this format:

```json
{
  "code": "ErrorCode",
  "message": "Human-readable error message",
  "errors": ["Additional error details"]
}
```

### Common Error Codes

| Code | Description |
|------|-------------|
| `InvalidCredentials` | Invalid username or password |
| `AccountLocked` | Account locked due to failed login attempts |
| `EmailNotConfirmed` | Email confirmation required |
| `EmailAlreadyExists` | Email already registered |
| `UserNotFound` | User does not exist |
| `TenantNotFound` | Tenant does not exist |
| `TenantAccessDenied` | User doesn't have access to tenant |
| `ValidationError` | Request validation failed |

---

## Rate Limiting

Rate limits are applied per IP address:

| Endpoint Pattern | Limit |
|------------------|-------|
| `POST /connect/token` | 5 requests per minute |
| `POST /api/account/register` | 3 requests per hour |
| `POST /api/account/forgot-password` | 3 requests per 15 minutes |
| `POST *` | 30 requests per minute |
| `PUT *` | 30 requests per minute |
| `DELETE *` | 20 requests per minute |
| `GET *` | 100 requests per minute |

Rate limit exceeded returns `429 Too Many Requests`.

---

## Pagination

List endpoints support pagination via query parameters:

- `page` - Page number (default: 1)
- `pageSize` - Items per page (default: 20, max: 100)

Response format:
```json
{
  "items": [...],
  "totalCount": 150,
  "page": 1,
  "pageSize": 20,
  "totalPages": 8
}
```

---

## Filtering and Search

### User Search
```
GET /api/users?email={email}&name={name}&isActive={bool}&tenantId={guid}
```

### Tenant Filtering
TenantAdmin automatically sees only their tenant's data. SuperAdmin can filter by tenant.

---

## Quick Start Examples

### Register and Login
```bash
# 1. Register
curl -X POST https://api.example.com/api/account/register \
  -H "Content-Type: application/json" \
  -d '{"email":"user@example.com","password":"Password123!","firstName":"John","lastName":"Doe"}'

# 2. Login
curl -X POST https://api.example.com/connect/token \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "grant_type=password&client_id=client&client_secret=secret&username=user@example.com&password=Password123!&scope=openid email profile api offline_access"
```

### Use API with Token
```bash
# Get current user profile
curl -X GET https://api.example.com/api/users/me \
  -H "Authorization: Bearer {access_token}"

# List tenants
curl -X GET https://api.example.com/api/tenants/my-tenants \
  -H "Authorization: Bearer {access_token}"
```

### Admin Operations
```bash
# List all users (SuperAdmin)
curl -X GET https://api.example.com/api/users \
  -H "Authorization: Bearer {admin_token}"

# Create tenant (SuperAdmin)
curl -X POST https://api.example.com/api/tenants \
  -H "Authorization: Bearer {admin_token}" \
  -H "Content-Type: application/json" \
  -d '{"name":"Acme Corp","slug":"acme-corp"}'
```

---

## Additional Resources

- [Authentication Guide](./auth/authentication-guide.md) - Complete authentication setup
- [User Management Guide](./USER_MANAGEMENT.md) - User administration details
- [Tenant Management Guide](./TENANT_MANAGEMENT.md) - Multi-tenancy details
- [README](../README.md) - Project setup and overview
