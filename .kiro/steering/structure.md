---
inclusion: always
---

# Project Structure

## Clean Architecture Layers

The project follows Clean Architecture principles with strict dependency rules:

```
MultiTenantETL/
├── src/
│   ├── MultiTenantETL.Domain/          # No dependencies
│   ├── MultiTenantETL.Application/     # Depends on Domain only
│   ├── MultiTenantETL.Infrastructure/  # Depends on Domain + Application
│   └── MultiTenantETL.API/             # Depends on all layers
└── docs/                                # Documentation
```

## Layer Responsibilities

### Domain Layer (MultiTenantETL.Domain)
- Pure business entities (Tenant)
- Domain interfaces (ITenantResource for authorization)
- Enums (AuthErrorCode)
- Constants (Roles, Permissions, Policies, ClaimTypes)
- No framework dependencies - pure C# business logic

### Application Layer (MultiTenantETL.Application)
- Service interfaces (IEmailService, ICurrentUserService, IClaimsService, ITenantService)
- DTOs and request/response models (LoginRequest, RegisterRequest, AuthResponse, UserDto, TenantDto)
- Common models (ErrorResponse, ErrorDetail)
- Business logic abstractions
- Depends only on Domain

### Infrastructure Layer (MultiTenantETL.Infrastructure)
- ApplicationDbContext (EF Core with PostgreSQL)
- Identity models (ApplicationUser, ApplicationRole, UserTenant)
- Service implementations (EmailService, ClaimsService, TenantService, CurrentUserService)
- Authorization handlers (PermissionAuthorizationHandler, TenantResourceAuthorizationHandler)
- Database migrations (EF Core)
- Data seeding (DbSeeder - roles, permissions, admin, OAuth clients)
- Security utilities (InputSanitizer)
- Configuration classes (AzureCommunicationSettings)

### API Layer (MultiTenantETL.API)
- Controllers (AuthenticationController, AccountController, UsersController, TenantsController)
- Middleware (SecurityHeadersMiddleware)
- Program.cs (dependency injection, configuration, OpenIddict setup)
- OpenIddict configuration (OAuth clients, scopes, token lifetimes)
- Rate limiting and CORS setup
- Swagger/OpenAPI documentation

## Key Folders

### Identity & Authentication
- `Domain/Constants/` - Roles (SuperAdmin, Admin, User), Permissions (UsersCreate, UsersRead, etc.), Policies, ClaimTypes
- `Infrastructure/Identity/` - ApplicationUser, ApplicationRole, UserTenant, CurrentUserService
- `Infrastructure/Services/` - ClaimsService, TenantService, EmailService implementations
- `API/Controllers/` - AuthenticationController (OAuth endpoints), AccountController (registration, password reset, logout)

### Authorization
- `Domain/Constants/Permissions.cs` - Permission constants (UsersCreate, UsersRead, UsersUpdate, UsersDelete, etc.)
- `Domain/Constants/Policies.cs` - Policy names (RequirePermission, RequireTenantAccess)
- `Infrastructure/Authorization/` - PermissionAuthorizationHandler, TenantResourceAuthorizationHandler, PermissionRequirement

### Database
- `Infrastructure/Persistence/` - ApplicationDbContext with entity configurations
- `Infrastructure/Migrations/` - EF Core migrations
- `Infrastructure/Data/` - DbSeeder for initial data (roles, permissions, admin user, OAuth clients, scopes)

### Configuration
- `Infrastructure/Configuration/` - Settings classes (AzureCommunicationSettings)
- `API/appsettings.json` - Application configuration
- `API/appsettings.Development.json` - Development overrides
- User secrets for sensitive data (connection strings, passwords, API keys)

## Naming Conventions

- **Entities**: PascalCase, singular (Tenant, ApplicationUser, ApplicationRole)
- **Interfaces**: Prefixed with 'I' (IEmailService, ITenantResource, ICurrentUserService)
- **DTOs**: Suffixed with purpose (LoginRequest, RegisterRequest, AuthResponse, UserDto, TenantDto)
- **Services**: Suffixed with 'Service' (ClaimsService, TenantService, EmailService)
- **Controllers**: Suffixed with 'Controller' (AuthenticationController, UsersController)
- **Database tables**: snake_case (users, roles, user_tenants, tenants)
- **Constants**: PascalCase in static classes (Roles.SuperAdmin, Permissions.UsersCreate, Policies.RequirePermission)
- **Authorization Handlers**: Suffixed with 'Handler' (PermissionAuthorizationHandler)

## Pragmatic Decisions

Authentication entities (ApplicationUser, ApplicationRole, UserTenant) live in Infrastructure rather than Domain because ASP.NET Core Identity is tightly coupled to Entity Framework. This pragmatic approach reduces complexity while maintaining clean separation for pure business entities like Tenant.

## Key Design Patterns

- **Repository Pattern**: Abstracted through EF Core DbContext
- **Service Layer**: Business logic in service implementations
- **Dependency Injection**: All services registered in Program.cs
- **Authorization Handlers**: Custom handlers for permission-based and resource-based authorization
- **Middleware Pipeline**: SecurityHeadersMiddleware for HTTP security headers
- **Seeding Pattern**: DbSeeder for initial data setup in development
