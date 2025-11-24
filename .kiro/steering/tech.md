---
inclusion: always
---

# Technology Stack

## Framework & Runtime

- .NET 8.0 SDK
- ASP.NET Core 8.0 Web API
- C# with nullable reference types enabled

## Database & ORM

- PostgreSQL 12+ (primary database)
- Entity Framework Core 8.0
- Npgsql.EntityFrameworkCore.PostgreSQL 8.0
- Database naming convention: snake_case for tables

## Authentication & Authorization

- **OpenIddict 7.2.0** (OAuth 2.0 & OpenID Connect server)
- **OAuth 2.0 Flows**:
  - **Authorization Code + PKCE** (recommended for SPAs) - RFC 7636 compliant
  - Password Grant (for API testing/machine-to-machine)
  - Refresh Token with rotation
- **OAuth Clients**:
  - `multitenant-etl-spa` - Public client for Vue.js frontend (no secret, PKCE required)
  - `multitenant-etl-postman` - Confidential client for API testing (with secret)
- **ASP.NET Core Identity** (user/role management)
- **Custom Authorization**: Permission-based handlers (PermissionAuthorizationHandler, TenantResourceAuthorizationHandler)
- **BCrypt.Net-Next 4.0.3** (password hashing)

## Key Libraries

- AspNetCoreRateLimit 5.0.0 (rate limiting on authentication endpoints)
- Azure.Communication.Email 1.1.0 (email service for confirmations, password resets)
- OpenIddict.Quartz 7.2.0 (background token cleanup)
- Microsoft.AspNetCore.Identity.EntityFrameworkCore 8.0

## Common Commands

### Build & Run

```bash
# Restore dependencies
dotnet restore

# Build solution
dotnet build

# Run API (from src/MultiTenantETL.API)
dotnet run

# Run with watch (auto-reload)
dotnet watch run
```

### Database Migrations

```bash
# Create new migration
dotnet ef migrations add MigrationName --project src/MultiTenantETL.Infrastructure --startup-project src/MultiTenantETL.API

# Apply migrations
dotnet ef database update --project src/MultiTenantETL.Infrastructure --startup-project src/MultiTenantETL.API

# Remove last migration (if not applied)
dotnet ef migrations remove --project src/MultiTenantETL.Infrastructure --startup-project src/MultiTenantETL.API

# List migrations
dotnet ef migrations list --project src/MultiTenantETL.Infrastructure --startup-project src/MultiTenantETL.API
```

### User Secrets (Development)

```bash
# Set user secrets (from src/MultiTenantETL.API)
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "your-connection-string"
dotnet user-secrets set "Seeding:AdminPassword" "your-admin-password"
dotnet user-secrets set "Seeding:OAuthClientSecret" "your-oauth-secret"
dotnet user-secrets set "AzureCommunication:ConnectionString" "your-azure-connection"
dotnet user-secrets set "AzureCommunication:SenderEmail" "noreply@yourdomain.com"

# List user secrets
dotnet user-secrets list
```

### Testing

```bash
# Run all tests
dotnet test

# Run with coverage
dotnet test /p:CollectCoverage=true
```

## Development Environment

- Default ports: HTTPS (7288), HTTP (5244)
- User secrets for sensitive configuration (UserSecretsId: 96149a75-7a4b-4db0-89c3-93fc63bf95e8)
- Swagger UI available in development mode at `/swagger`
- Database seeding runs automatically in development (roles, permissions, admin user, OAuth clients)
- CORS configured for `http://localhost:5173` (Vue dev server)

## Security Configuration

- **Token Lifetimes**: Access tokens (15 min), Refresh tokens (7 days)
- **Account Lockout**: 5 failed attempts, 15-minute lockout duration
- **Password Requirements**: Min 8 chars, uppercase, lowercase, digit, special character
- **Rate Limiting**: Configured on authentication endpoints
- **Security Headers**: X-Content-Type-Options, X-Frame-Options, X-XSS-Protection, Referrer-Policy
