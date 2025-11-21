# Authentication & Authorization Guide

**Version:** 3.0 - Modular Structure  
**Last Updated:** 2025-11-20

---

## Overview

Complete authentication and authorization system for Multi-Tenant ETL platform using:
- **OpenIddict** - OAuth 2.0 & OpenID Connect
- **ASP.NET Core Identity** - User management
- **Azure Communication Services** - Email delivery
- **Permission-based authorization** - Role + claim based access control

---

## Documentation Structure

This guide is split into focused modules for easier AI parsing (each file < 500 lines):

### **Architecture**
0. **[Clean Architecture Structure](./0-clean-architecture.md)**
   - Project organization (Domain, Application, Infrastructure, API)
   - Layer dependencies and responsibilities
   - Folder structure and file placement
   - CQRS pattern with MediatR
   - Dependency injection per layer

### **Core Authentication**
1. **[Setup & Configuration](./1-setup.md)**
   - NuGet packages installation
   - Database models (ApplicationUser, ApplicationRole, UserTenant)
   - OpenIddict configuration
   - Error codes and enums
   - Application settings

2. **[Email Service](./2-email-service.md)**
   - Azure Communication Services integration
   - Email templates (confirmation, reset, welcome)
   - Development mode (logging instead of sending)

3. **[Controllers](./3-controllers.md)**
   - AuthenticationController (login, token refresh)
   - AccountController (registration, email confirmation, password reset)
   - All DTOs and request/response models

4. **[Roles & Claims](./4-roles-claims.md)**
   - Role seeding (Admin, Manager, User)
   - Role assignment to users
   - JWT claims population
   - CurrentUserService implementation
   - Permission checking

### **Security & Authorization**
5. **[Security Features](./5-security.md)**
   - Rate limiting (endpoint-specific)
   - CORS configuration
   - Security headers (HSTS, CSP, etc.)
   - Input validation and sanitization
   - Secrets management

6. **[Authorization](./6-authorization.md)**
   - Permission matrix (complete resource permissions)
   - Authorization handlers and policies
   - Tenant isolation implementation
   - Resource-based authorization
   - Special cases (self-service, cross-tenant)

---

## Quick Start

### Minimal Setup (Authentication Only)
Read in this order:
1. [Setup & Configuration](./1-setup.md) - Get database and OpenIddict running
2. [Controllers](./3-controllers.md) - Implement login and registration
3. [Email Service](./2-email-service.md) - Add email confirmation

**Result:** Working login, registration, and password reset

### Full Setup (Authentication + Authorization)
Add these after minimal setup:
4. [Roles & Claims](./4-roles-claims.md) - Add role-based access
5. [Authorization](./6-authorization.md) - Implement permissions
6. [Security Features](./5-security.md) - Harden for production

**Result:** Complete, production-ready auth/authz system

---

## Key Features

### Authentication
✅ User registration with email confirmation  
✅ Login (username/password)  
✅ Password reset flow  
✅ JWT access tokens (15 min) + refresh tokens (7 days)  
✅ Multi-tenant support  
✅ Tenant switching  

### Authorization
✅ Role-based access control (Admin, Manager, User)  
✅ Permission-based authorization (resource:action)  
✅ Wildcard permissions (pipelines:*, *:read)  
✅ Tenant isolation (users only access their tenant data)  
✅ Resource-based authorization  

### Security
✅ Account lockout (5 failed attempts)  
✅ Rate limiting (per endpoint)  
✅ CORS configuration  
✅ Security headers (HSTS, CSP, X-Frame-Options)  
✅ Password complexity requirements  
✅ Email enumeration prevention  

---

## Implementation Checklist

### Phase 1: Setup (File 1)
- [ ] Install NuGet packages
- [ ] Create database models
- [ ] Configure OpenIddict
- [ ] Run migrations
- [ ] Seed OpenIddict clients

### Phase 2: Authentication (Files 2-3)
- [ ] Implement email service
- [ ] Create AuthenticationController
- [ ] Create AccountController
- [ ] Test registration flow
- [ ] Test login flow
- [ ] Test password reset

### Phase 3: Authorization (File 4-6)
- [ ] Seed roles
- [ ] Implement role assignment
- [ ] Add claims to JWT
- [ ] Create CurrentUserService
- [ ] Implement authorization handlers
- [ ] Test permission checking

### Phase 4: Security (File 5)
- [ ] Configure rate limiting
- [ ] Set up CORS
- [ ] Add security headers
- [ ] Implement input validation
- [ ] Configure secrets management

---

## API Endpoints Summary

### Authentication (OpenIddict)
- `POST /connect/token` - Login, refresh tokens
- `POST /connect/authorize` - Authorization code flow
- `POST /connect/logout` - Logout

### Account Management
- `POST /api/account/register` - User registration
- `POST /api/account/confirm-email` - Email confirmation
- `POST /api/account/forgot-password` - Request password reset
- `POST /api/account/reset-password` - Reset password
- `POST /api/account/change-password` - Change password (authenticated)
- `POST /api/account/logout` - Logout
- `POST /api/account/switch-tenant` - Switch active tenant

---

## Testing

### Quick Test Commands

**Register:**
```bash
curl -X POST http://localhost:5000/api/account/register \
  -H "Content-Type: application/json" \
  -d '{"email":"test@example.com","password":"Test@123456","firstName":"Test","lastName":"User"}'
```

**Login:**
```bash
curl -X POST http://localhost:5000/connect/token \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "grant_type=password&username=test@example.com&password=Test@123456&scope=openid email profile api"
```

**Use Token:**
```bash
curl -X GET http://localhost:5000/api/pipelines \
  -H "Authorization: Bearer YOUR_ACCESS_TOKEN"
```

---

## Configuration Summary

### Required Settings (appsettings.json)
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=multitenanteti;..."
  },
  "AppSettings": {
    "FrontendUrl": "http://localhost:5173"
  },
  "Authentication": {
    "RequireEmailConfirmation": true
  },
  "AzureCommunicationServices": {
    "ConnectionString": "endpoint=https://...",
    "FromEmail": "DoNotReply@yourdomain.com"
  }
}
```

---

## Roles & Permissions Overview

### Roles
- **Admin** - Full control within tenant
- **Manager** - Manage resources and users
- **User** - View and execute

### Permission Format
```
{resource}:{action}
```

Examples: `pipelines:create`, `users:delete`, `connectors:*`, `*:read`

See [Authorization](./6-authorization.md) for complete matrix.

---

## JWT Token Structure

```json
{
  "sub": "user-guid",
  "email": "user@example.com",
  "name": "John Doe",
  "role": "admin",
  "tenant_id": "tenant-guid",
  "tenant_name": "Acme Corp",
  "permissions": "[\"users:*\",\"pipelines:*\",...]",
  "exp": 1234567890
}
```

---

## Troubleshooting

### Common Issues

**OpenIddict tables not created**
- Run migrations after adding OpenIddict configuration

**Emails not sending**
- Check Azure Communication Services connection string
- In development, emails are logged instead of sent

**CORS errors**
- Verify frontend URL in CORS settings
- Ensure `UseCors()` before `UseAuthorization()`

**Permission denied errors**
- Check user role has required permission
- Verify claims are populated in JWT
- Check tenant isolation

---

## Next Steps

1. Start with **[Clean Architecture Structure](./0-clean-architecture.md)** - Understand project organization
2. Then **[Setup & Configuration](./1-setup.md)** - Install and configure
3. Follow the implementation checklist above
4. Test each phase before moving to the next
5. Refer to specific files as needed

---

## File Index

| File | Lines | Purpose |
|------|-------|---------|
| [0-clean-architecture.md](./0-clean-architecture.md) | ~500 | Clean Architecture structure & layers |
| [1-setup.md](./1-setup.md) | ~450 | Installation, database, OpenIddict |
| [2-email-service.md](./2-email-service.md) | ~300 | Email implementation |
| [3-controllers.md](./3-controllers.md) | ~500 | Authentication & account controllers |
| [4-roles-claims.md](./4-roles-claims.md) | ~480 | Roles, claims, permissions |
| [5-security.md](./5-security.md) | ~470 | Rate limiting, CORS, security |
| [6-authorization.md](./6-authorization.md) | ~490 | Authorization handlers, permissions matrix |

**Total:** ~3,200 lines split across 7 focused files

---

Ready to implement! Start with file 1 and work your way through. 🚀
