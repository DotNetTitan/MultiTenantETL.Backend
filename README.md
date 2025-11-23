# MultiTenant ETL - Multi-Tenant ASP.NET Core Web API

A production-ready, secure multi-tenant ASP.NET Core 8.0 Web API with OpenIddict OAuth 2.0/OpenID Connect authentication.

## 🚀 Features

- **Multi-Tenancy**: Complete tenant isolation with per-user tenant switching
  - Automatic personal workspace creation on registration
  - Full tenant CRUD operations
  - User-tenant relationship management
  - Role-based access within tenants
- **OAuth 2.0 & OpenID Connect**: Powered by OpenIddict 7.2.0
- **Authentication Flows**:
  - Password Grant (for API testing/machine-to-machine)
  - Authorization Code + PKCE (for SPAs)
  - Refresh Token support
- **Token Management**: Token refresh and revocation endpoints
- **ASP.NET Core Identity**: User and role management with custom claims
- **PostgreSQL**: Entity Framework Core with database migrations
- **Email Integration**: Welcome emails, password reset, email confirmation
- **Security**: Token revocation on password change, email confirmation

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

Copy the example configuration file:

```bash
cp src/MultiTenantETL.API/appsettings.Development.json.example src/MultiTenantETL.API/appsettings.Development.json
```

Edit `appsettings.Development.json` with your settings:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=MultiTenantETL;Username=YOUR_USERNAME;Password=YOUR_PASSWORD"
  },
  "Seeding": {
    "AdminPassword": "YOUR_SECURE_ADMIN_PASSWORD",
    "OAuthClientSecret": "YOUR_OAUTH_CLIENT_SECRET"
  }
}
```

### 4. Run Database Migrations

```bash
cd src/MultiTenantETL.API
dotnet ef database update --project ../MultiTenantETL.Infrastructure/MultiTenantETL.Infrastructure.csproj
```

### 5. Run the Application

```bash
dotnet run
```

The API will be available at:
- HTTPS: `https://localhost:7288`
- HTTP: `http://localhost:5244`

### 6. Default Admin Account

After seeding, you can log in with:
- **Email**: `admin@multitenant-etl.com`
- **Password**: The password you set in `appsettings.Development.json` under `Seeding:AdminPassword`

## 🏗️ Architecture

The project follows **Clean Architecture** principles:

```
MultiTenantETL/
├── src/
│   ├── MultiTenantETL.API/          # Web API & Controllers
│   ├── MultiTenantETL.Application/  # DTOs, Business Logic
│   ├── MultiTenantETL.Domain/       # Entities, Interfaces
│   └── MultiTenantETL.Infrastructure/ # EF Core, Identity, Data Access
```

### Key Components

- **API Layer**: Controllers, OpenIddict configuration, middleware
- **Application Layer**: Request/response models, service interfaces
- **Domain Layer**: Entity models (User, Tenant, UserTenant, etc.)
- **Infrastructure Layer**: DbContext, Identity, database seeding, migrations

## 🔐 Authentication & Authorization

### OAuth Clients

Two OAuth clients are seeded by default:

#### 1. SPA Client (Public)
- **Client ID**: `multitenant-etl-spa`
- **Type**: Public client (no secret)
- **Flow**: Authorization Code + PKCE
- **Redirect URIs**: 
  - `http://localhost:5173/auth/callback`
  - `https://app.example.com/auth/callback`

#### 2. Postman/Testing Client (Confidential)
- **Client ID**: `multitenant-etl-postman`
- **Client Secret**: Set in `appsettings` under `Seeding:OAuthClientSecret`
- **Flow**: Password Grant, Refresh Token
- **Use Case**: API testing, machine-to-machine

### Available Scopes

- `openid` - OpenID Connect authentication
- `email` - User's email address
- `profile` - User's profile info (name, etc.)
- `roles` - User's roles
- `api` - Access to API resources
- `offline_access` - Refresh token support

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

## 🧪 Testing with Postman

### 1. Login (Password Grant)

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

### 2. Refresh Token

```http
POST https://localhost:7288/connect/token
Content-Type: application/x-www-form-urlencoded

grant_type=refresh_token
&client_id=multitenant-etl-postman
&client_secret=YOUR_CLIENT_SECRET
&refresh_token=YOUR_REFRESH_TOKEN
```

### 3. Revoke Token

```http
POST https://localhost:7288/connect/revoke
Content-Type: application/x-www-form-urlencoded
Authorization: Bearer YOUR_ACCESS_TOKEN

token=YOUR_REFRESH_TOKEN
&token_type_hint=refresh_token
```

## 🔒 Security Features

- ✅ Password hashing with ASP.NET Core Identity
- ✅ Email confirmation required for new accounts
- ✅ Refresh token rotation
- ✅ Token revocation on password change
- ✅ Token revocation on logout
- ✅ Account lockout after failed login attempts
- ✅ Password validation (min 8 chars, complexity requirements)
- ✅ Short-lived access tokens (15 minutes)
- ✅ Long-lived refresh tokens (7 days)

## 🗄️ Database Schema

### Key Tables

- `users` - ASP.NET Identity users with multi-tenant support
- `roles` - Application roles with custom permissions
- `Tenants` - Tenant organizations
- `UserTenants` - Many-to-many relationship between users and tenants
- `OpenIddictApplications` - OAuth clients
- `OpenIddictTokens` - Issued tokens (for refresh token storage)
- `OpenIddictAuthorizations` - Authorization grants
- `OpenIddictScopes` - Available OAuth scopes

## 📧 Email Configuration

The application requires email service configuration for:
- Welcome emails
- Email confirmation
- Password reset
- Password change notifications

Configure your email service in `appsettings.json` (implementation pending).

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

### Running Migrations

Create a new migration:

```bash
dotnet ef migrations add MigrationName --project src/MultiTenantETL.Infrastructure --startup-project src/MultiTenantETL.API
```

Apply migrations:

```bash
dotnet ef database update --project src/MultiTenantETL.Infrastructure --startup-project src/MultiTenantETL.API
```

### Project Structure

- **Controllers**: API endpoints and request handling
- **Migrations**: EF Core database migrations
- **DbSeeder**: Initial data seeding (roles, admin, OAuth clients)
- **Identity**: Custom ApplicationUser and ApplicationRole models
- **Entities**: Domain models (Tenant, UserTenant)

## 📝 License

[Specify your license here]

## 🤝 Contributing

[Add contribution guidelines if open source]

## 📞 Support

For questions or issues, please [open an issue](link-to-issues) on GitHub.
