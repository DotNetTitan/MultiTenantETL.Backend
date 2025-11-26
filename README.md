# MultiTenant ETL - Multi-Tenant ASP.NET Core Web API

A production-ready, secure multi-tenant ASP.NET Core 8.0 Web API designed for ETL (Extract, Transform, Load) operations with complete tenant isolation, OAuth 2.0/OpenID Connect authentication powered by OpenIddict, and permission-based authorization.

## 🚀 Features

- **Multi-Tenancy**: Complete tenant isolation with per-user tenant switching
  - Automatic personal workspace creation on registration
  - Full tenant CRUD operations with SuperAdmin/Admin controls
  - User-tenant relationship management
  - Role-based access within tenants (SuperAdmin, Admin, User)
- **OAuth 2.0 & OpenID Connect**: Powered by OpenIddict 7.2.0
  - **Authorization Code + PKCE** (recommended for SPAs) - RFC 7636 compliant
  - Password Grant (for API testing/machine-to-machine)
  - Refresh Token support with rotation
  - Single-use authorization codes with state parameter for CSRF protection
- **Token Management**: 
  - Short-lived access tokens (15 minutes)
  - Long-lived refresh tokens (7 days)
  - Token refresh and revocation endpoints
  - Automatic token cleanup with Quartz background jobs
- **ASP.NET Core Identity**: User and role management with custom claims
- **Permission-Based Authorization**: Fine-grained access control with custom authorization handlers
  - PermissionAuthorizationHandler for permission-based policies
  - TenantResourceAuthorizationHandler for resource-based authorization
- **PostgreSQL**: Entity Framework Core 8.0 with database migrations and snake_case naming
- **Email Integration**: Azure Communication Services for welcome emails, password reset, email confirmation
- **Security**: 
  - BCrypt password hashing
  - Account lockout (5 failed attempts, 15-minute lockout)
  - Rate limiting on authentication endpoints (AspNetCoreRateLimit)
  - CORS configuration for frontend origins
  - Security headers middleware
  - Token revocation on password change and logout
  - Input sanitization utilities
  - Email enumeration prevention
- **Clean Architecture**: Strict separation of concerns with Domain, Application, Infrastructure, and API layers

## 📋 Prerequisites

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [PostgreSQL 12+](https://www.postgresql.org/download/)
- A code editor ([VS Code](https://code.visualstudio.com/), [Visual Studio](https://visualstudio.microsoft.com/), or [Rider](https://www.jetbrains.com/rider/))

## 🛠️ Setup Instructions

### 1. Clone the Repository

```bash
git clone <repository-url>
cd MultiTenantETL
```

### 2. Configure Database

Create a PostgreSQL database:

```sql
CREATE DATABASE "MultiTenantETL";
```

### 3. Configure Application Settings

Use **user secrets** for sensitive configuration (recommended for development):

```bash
cd src/MultiTenantETL.API

# Set database connection string
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=MultiTenantETL;Username=YOUR_USERNAME;Password=YOUR_PASSWORD"

# Set admin password for seeding
dotnet user-secrets set "Seeding:AdminPassword" "YOUR_SECURE_ADMIN_PASSWORD"

# Set OAuth client secret for testing
dotnet user-secrets set "Seeding:OAuthClientSecret" "YOUR_OAUTH_CLIENT_SECRET"

# Set Azure Communication Services (for email)
dotnet user-secrets set "AzureCommunication:ConnectionString" "your-azure-connection-string"
dotnet user-secrets set "AzureCommunication:SenderEmail" "noreply@yourdomain.com"
```

**User Secrets ID**: `96149a75-7a4b-4db0-89c3-93fc63bf95e8`

Alternatively, edit `appsettings.Development.json` (not recommended for sensitive data):

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=MultiTenantETL;Username=YOUR_USERNAME;Password=YOUR_PASSWORD"
  },
  "Seeding": {
    "AdminPassword": "YOUR_SECURE_ADMIN_PASSWORD",
    "OAuthClientSecret": "YOUR_OAUTH_CLIENT_SECRET"
  },
  "AzureCommunication": {
    "ConnectionString": "your-azure-connection-string",
    "SenderEmail": "noreply@yourdomain.com"
  }
}
```

### 4. Run Database Migrations

```bash
# From the project root
dotnet ef database update --project src/MultiTenantETL.Infrastructure --startup-project src/MultiTenantETL.API

# Or from src/MultiTenantETL.API
cd src/MultiTenantETL.API
dotnet ef database update --project ../MultiTenantETL.Infrastructure
```

This will create all necessary tables and seed initial data (roles, permissions, admin user, OAuth clients).

### 5. Run the Application

```bash
# From src/MultiTenantETL.API
cd src/MultiTenantETL.API
dotnet run

# Or with auto-reload during development
dotnet watch run
```

The API will be available at:
- **HTTPS**: `https://localhost:7288`
- **HTTP**: `http://localhost:5244`
- **Swagger UI**: `https://localhost:7288/swagger` (development only)

### 6. Default Admin Account

After seeding, you can log in with:
- **Email**: `admin@multitenant-etl.com`
- **Password**: The password you set in user secrets under `Seeding:AdminPassword`
- **Role**: SuperAdmin (full system access)

## 🏗️ Architecture

The project follows **Clean Architecture** principles with strict dependency rules:

```
MultiTenantETL/
├── src/
│   ├── MultiTenantETL.Domain/          # No dependencies - Pure business entities
│   ├── MultiTenantETL.Application/     # Depends on Domain only
│   ├── MultiTenantETL.Infrastructure/  # Depends on Domain + Application
│   └── MultiTenantETL.API/             # Depends on all layers
└── docs/                                # Documentation
```

### Layer Responsibilities

- **Domain Layer**: Pure business entities (Tenant), domain interfaces (ITenantResource), enums, constants (Roles, Permissions, Policies)
- **Application Layer**: Service interfaces (IEmailService, ICurrentUserService), DTOs, request/response models, business logic abstractions
- **Infrastructure Layer**: ApplicationDbContext, Identity models (ApplicationUser, ApplicationRole, UserTenant), service implementations, authorization handlers, migrations, data seeding
- **API Layer**: Controllers (Authentication, Account, Users, Tenants), middleware (SecurityHeadersMiddleware), OpenIddict configuration, rate limiting, CORS

### Key Design Decisions

- **Pragmatic Architecture**: Authentication entities (ApplicationUser, ApplicationRole, UserTenant) live in Infrastructure rather than Domain due to tight coupling with ASP.NET Core Identity and Entity Framework. This reduces complexity while maintaining clean separation for pure business entities like Tenant.
- **Permission-Based Authorization**: Custom handlers (PermissionAuthorizationHandler, TenantResourceAuthorizationHandler) for fine-grained access control
- **Tenant Context**: Managed through claims (TenantId claim in JWT tokens) for seamless tenant switching
- **Database Naming**: snake_case convention for tables (e.g., `users`, `roles`, `user_tenants`, `tenants`)
- **Token Cleanup**: Quartz background jobs (OpenIddict.Quartz) for automatic token cleanup

## 🔐 Authentication & Authorization

### OAuth Clients

Two OAuth clients are seeded automatically by DbSeeder:

#### 1. SPA Client (Public) - **Recommended for Frontend**
- **Client ID**: `multitenant-etl-spa`
- **Type**: Public client (no client secret required)
- **Flow**: Authorization Code + PKCE (RFC 7636 compliant)
- **Security**: 
  - PKCE prevents authorization code interception attacks
  - Single-use authorization codes
  - State parameter for CSRF protection
  - Code verifier proves authorization request origin
- **Use Case**: Vue.js frontend, React apps, Angular apps, any SPA
- **Redirect URIs**: 
  - `http://localhost:5173/auth/callback` (development)
  - `https://app.example.com/auth/callback` (production - update in DbSeeder)

#### 2. Postman/Testing Client (Confidential)
- **Client ID**: `multitenant-etl-postman`
- **Client Secret**: Set in user secrets under `Seeding:OAuthClientSecret`
- **Flow**: Password Grant, Refresh Token
- **Use Case**: API testing with Postman, machine-to-machine communication

### Available Scopes

- `openid` - OpenID Connect authentication (required)
- `email` - User's email address
- `profile` - User's profile info (name, etc.)
- `roles` - User's roles (SuperAdmin, Admin, User)
- `api` - Access to API resources
- `offline_access` - Refresh token support (7-day lifetime)

### User Roles & Permissions

#### Roles
- **SuperAdmin**: System-wide administration, tenant management, user management across all tenants
- **Admin**: Tenant-level administration, user management within tenant, full pipeline operations
- **User**: Access to pipelines, connectors, transformations, and executions within their tenant

#### Permissions (defined in `Domain/Constants/Permissions.cs`)
- **Users**: `UsersCreate`, `UsersRead`, `UsersUpdate`, `UsersDelete`
- **Tenants**: `TenantsCreate`, `TenantsRead`, `TenantsUpdate`, `TenantsDelete`
- Additional permissions can be added for pipelines, connectors, etc.

#### Policies (defined in `Domain/Constants/Policies.cs`)
- `RequirePermission` - Permission-based authorization
- `RequireTenantAccess` - Resource-based authorization for tenant access

## 📡 API Endpoints

### Authentication Endpoints

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/connect/token` | POST | OAuth token endpoint (login, refresh) |
| `/connect/authorize` | GET/POST | OAuth authorization endpoint (for SPAs) |
| `/connect/revoke` | POST | Token revocation endpoint |

### Account Management

| Endpoint | Method | Auth | Description |
|----------|--------|------|-------------|
| `/api/account/register` | POST | No | Register new user |
| `/api/account/confirm-email` | POST | No | Confirm email address |
| `/api/account/forgot-password` | POST | No | Request password reset |
| `/api/account/reset-password` | POST | No | Reset password with token |
| `/api/account/change-password` | POST | Yes | Change current password |
| `/api/account/logout` | POST | Yes | Logout and revoke tokens |
| `/api/account/switch-tenant` | POST | Yes | Switch active tenant |

### User Management

| Endpoint | Method | Auth | Description |
|----------|--------|------|-------------|
| `/api/users/me` | GET | Yes | Get current user profile |
| `/api/users/me` | PUT | Yes | Update current user profile |
| `/api/users` | GET | Admin | List/search users |
| `/api/users/{id}` | GET | Admin | Get user by ID |
| `/api/users/{id}` | PUT | SuperAdmin | Update user |
| `/api/users/{id}/status` | PUT | SuperAdmin | Activate/deactivate user |
| `/api/users/{id}` | DELETE | SuperAdmin | Delete user |
| `/api/users/{id}/roles` | POST | SuperAdmin | Assign role to user |
| `/api/users/{id}/roles` | DELETE | SuperAdmin | Remove role from user |
| `/api/users/{id}/reset-password` | POST | SuperAdmin | Admin password reset |

### Tenant Management

| Endpoint | Method | Auth | Description |
|----------|--------|------|-------------|
| `/api/tenants` | GET | SuperAdmin | List all tenants |
| `/api/tenants/my-tenants` | GET | Yes | Get current user's tenants |
| `/api/tenants/{id}` | GET | Yes | Get tenant by ID |
| `/api/tenants` | POST | SuperAdmin | Create tenant |
| `/api/tenants/{id}` | PUT | Admin | Update tenant |
| `/api/tenants/{id}` | DELETE | SuperAdmin | Delete tenant |
| `/api/tenants/{id}/users` | GET | Admin | List tenant users |
| `/api/tenants/{id}/users` | POST | Admin | Add user to tenant |
| `/api/tenants/{tenantId}/users/{userId}` | DELETE | Admin | Remove user from tenant |
| `/api/tenants/{tenantId}/users/{userId}/role` | PUT | Admin | Update user role in tenant |

## 🧪 Testing

### Testing with SPA (Authorization Code + PKCE) - **Recommended**

The recommended way to test is through the Vue.js frontend:

1. Start the API: 
   ```bash
   cd src/MultiTenantETL.API
   dotnet run
   ```

2. Start the frontend (in a separate terminal):
   ```bash
   cd MultiTenantETL.Vue
   npm run dev
   ```

3. Navigate to `http://localhost:5173/login`

4. Login with:
   - **Email**: `admin@multitenant-etl.com`
   - **Password**: Your `Seeding:AdminPassword` from user secrets

The frontend implements the full OAuth 2.0 Authorization Code Flow with PKCE:
- Login → Browser redirect → Authorization → Callback → Token exchange → Dashboard
- PKCE utilities in `src/utils/pkce.js` generate code verifier/challenge
- See `MultiTenantETL.Vue/docs/OAUTH_PKCE.md` for detailed implementation

### Testing with Postman (Password Grant)

For API testing and development, use the password grant flow:

#### 1. Login (Password Grant)

```http
POST https://localhost:7288/connect/token
Content-Type: application/x-www-form-urlencoded

grant_type=password
&client_id=multitenant-etl-postman
&client_secret=YOUR_CLIENT_SECRET
&username=admin@multitenant-etl.com
&password=YOUR_ADMIN_PASSWORD
&scope=openid email profile roles api offline_access
```

#### 2. Refresh Token

```http
POST https://localhost:7288/connect/token
Content-Type: application/x-www-form-urlencoded

grant_type=refresh_token
&client_id=multitenant-etl-postman
&client_secret=YOUR_CLIENT_SECRET
&refresh_token=YOUR_REFRESH_TOKEN
```

#### 3. Revoke Token

```http
POST https://localhost:7288/connect/revoke
Content-Type: application/x-www-form-urlencoded
Authorization: Bearer YOUR_ACCESS_TOKEN

token=YOUR_REFRESH_TOKEN
&token_type_hint=refresh_token
```

### Testing Authorization Code + PKCE with cURL

See `MultiTenantETL.Vue/docs/OAUTH_PKCE.md` for manual testing instructions with cURL.

## 🔒 Security Features

### Authentication & Authorization
- ✅ **OAuth 2.0 PKCE**: Proof Key for Code Exchange prevents authorization code interception (RFC 7636)
- ✅ **Public Client Support**: No client secret required for SPAs
- ✅ **Single-use Authorization Codes**: Codes can only be exchanged once for tokens
- ✅ **State Parameter**: CSRF protection for OAuth flows
- ✅ **BCrypt Password Hashing**: Secure password storage with BCrypt.Net-Next 4.0.3
- ✅ **Permission-Based Authorization**: Custom handlers for fine-grained access control
- ✅ **Resource-Based Authorization**: TenantResourceAuthorizationHandler for tenant access

### Token Management
- ✅ **Short-lived Access Tokens**: 15-minute lifetime
- ✅ **Long-lived Refresh Tokens**: 7-day lifetime with rotation
- ✅ **Token Revocation**: Automatic revocation on password change and logout
- ✅ **Background Cleanup**: Quartz jobs for expired token cleanup

### Account Security
- ✅ **Email Confirmation**: Required for new accounts
- ✅ **Account Lockout**: 5 failed attempts, 15-minute lockout duration
- ✅ **Password Requirements**: Min 8 chars, uppercase, lowercase, digit, special character
- ✅ **Email Enumeration Prevention**: Consistent responses for security

### API Security
- ✅ **Rate Limiting**: AspNetCoreRateLimit 5.0.0 on authentication endpoints
- ✅ **CORS Configuration**: Configured for `http://localhost:5173` (Vue dev server)
- ✅ **Security Headers**: X-Content-Type-Options, X-Frame-Options, X-XSS-Protection, Referrer-Policy
- ✅ **Input Sanitization**: Utilities for preventing injection attacks

## 🗄️ Database Schema

### Key Tables (snake_case naming convention)

- `users` - ASP.NET Identity users with multi-tenant support (ApplicationUser)
- `roles` - Application roles (ApplicationRole) with custom permissions
- `tenants` - Tenant organizations (Tenant entity in Domain layer)
- `user_tenants` - Many-to-many relationship between users and tenants (UserTenant)
- `OpenIddictApplications` - OAuth clients (multitenant-etl-spa, multitenant-etl-postman)
- `OpenIddictTokens` - Issued tokens (access tokens, refresh tokens)
- `OpenIddictAuthorizations` - Authorization grants
- `OpenIddictScopes` - Available OAuth scopes (openid, email, profile, roles, api, offline_access)

### Migrations

All migrations are located in `src/MultiTenantETL.Infrastructure/Migrations/` and managed by Entity Framework Core 8.0.

## 📧 Email Configuration

The application uses **Azure Communication Services** for email delivery:
- Welcome emails on registration
- Email confirmation links
- Password reset tokens
- Password change notifications

Configure in `appsettings.json`:

```json
{
  "AzureCommunication": {
    "ConnectionString": "your-azure-communication-connection-string",
    "SenderEmail": "noreply@yourdomain.com"
  }
}
```

Or use user secrets for development:

```bash
dotnet user-secrets set "AzureCommunication:ConnectionString" "your-connection-string"
dotnet user-secrets set "AzureCommunication:SenderEmail" "noreply@yourdomain.com"
```

## 🚢 Deployment

### Production Checklist

- [ ] Change all default passwords and secrets
- [ ] Configure production database connection string
- [ ] Set up SSL certificates for token signing/encryption
- [ ] Configure CORS for your frontend origin
- [ ] Set up email service (SendGrid, AWS SES, etc.)
- [ ] Configure logging and monitoring
- [ ] Set up health checks
- [ ] Review and update OAuth client redirect URIs
- [ ] Enable rate limiting on auth endpoints
- [ ] Configure backup strategy for database

### Environment Variables

For production, use environment variables instead of appsettings:

```bash
ConnectionStrings__DefaultConnection="your-connection-string"
Seeding__AdminPassword="your-secure-password"
Seeding__OAuthClientSecret="your-oauth-secret"
```

## 🛠️ Development

### Common Commands

```bash
# Restore dependencies
dotnet restore

# Build solution
dotnet build

# Run API (from src/MultiTenantETL.API)
dotnet run

# Run with watch (auto-reload)
dotnet watch run

# Run all tests
dotnet test
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

### User Secrets Management

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

### Project Components

#### API Layer (`src/MultiTenantETL.API`)
- **Controllers**: 
  - `AuthenticationController` - OAuth 2.0 endpoints (not used directly, handled by OpenIddict)
  - `AccountController` - Registration, password reset, email confirmation, logout, tenant switching
  - `UsersController` - User management CRUD operations
  - `TenantsController` - Tenant management CRUD operations
- **Middleware**: `SecurityHeadersMiddleware` for HTTP security headers
- **Configuration**: `Program.cs` with OpenIddict setup, DI, rate limiting, CORS

#### Infrastructure Layer (`src/MultiTenantETL.Infrastructure`)
- **Persistence**: `ApplicationDbContext` with EF Core and PostgreSQL
- **Identity**: `ApplicationUser`, `ApplicationRole`, `UserTenant` models
- **Services**: `EmailService`, `ClaimsService`, `TenantService`, `CurrentUserService`
- **Authorization**: `PermissionAuthorizationHandler`, `TenantResourceAuthorizationHandler`
- **Data**: `DbSeeder` for initial data (roles, permissions, admin user, OAuth clients, scopes)
- **Migrations**: EF Core migrations
- **Security**: `InputSanitizer` utilities

#### Application Layer (`src/MultiTenantETL.Application`)
- **Interfaces**: `IEmailService`, `ICurrentUserService`, `IClaimsService`, `ITenantService`
- **DTOs**: `LoginRequest`, `RegisterRequest`, `AuthResponse`, `UserDto`, `TenantDto`
- **Models**: `ErrorResponse`, `ErrorDetail`

#### Domain Layer (`src/MultiTenantETL.Domain`)
- **Entities**: `Tenant` (pure business entity)
- **Interfaces**: `ITenantResource` (for authorization)
- **Constants**: `Roles`, `Permissions`, `Policies`, `ClaimTypes`
- **Enums**: `AuthErrorCode`

## 📝 License

[Specify your license here]

## 🤝 Contributing

[Add contribution guidelines if open source]

## 📞 Support

For questions or issues, please [open an issue](link-to-issues) on GitHub.
