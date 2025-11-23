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
- Pure business entities (Tenant, etc.)
- Domain interfaces (ITenantResource)
- Enums (AuthErrorCode)
- Constants (Roles, Permissions, Policies, ClaimTypes)
- No framework dependencies

### Application Layer (MultiTenantETL.Application)
- Service interfaces (IEmailService, ICurrentUserService)
- DTOs and request/response models
- Common models (ErrorResponse, ErrorDetail)
- Business logic abstractions
- Depends only on Domain

### Infrastructure Layer (MultiTenantETL.Infrastructure)
- ApplicationDbContext (EF Core)
- Identity models (ApplicationUser, ApplicationRole, UserTenant)
- Service implementations (EmailService, ClaimsService, TenantService)
- Authorization handlers (PermissionAuthorizationHandler, TenantResourceAuthorizationHandler)
- Database migrations
- Data seeding (DbSeeder)
- Security utilities (InputSanitizer)

### API Layer (MultiTenantETL.API)
- Controllers (AuthenticationController, AccountController)
- Middleware (SecurityHeadersMiddleware)
- Program.cs (dependency injection, configuration)
- OpenIddict configuration
- Rate limiting and CORS setup

## Key Folders

### Identity & Authentication
- `Infrastructure/Identity/` - ApplicationUser, ApplicationRole, UserTenant, CurrentUserService
- `Infrastructure/Services/` - ClaimsService, TenantService, EmailService implementations
- `API/Controllers/` - AuthenticationController (OAuth endpoints), AccountController (registration, password reset)

### Authorization
- `Domain/Constants/` - Roles, Permissions, Policies, ClaimTypes
- `Infrastructure/Authorization/` - Authorization handlers and requirements

### Database
- `Infrastructure/Persistence/` - ApplicationDbContext
- `Infrastructure/Migrations/` - EF Core migrations
- `Infrastructure/Data/` - DbSeeder for initial data

### Configuration
- `Infrastructure/Configuration/` - Settings classes (AzureCommunicationSettings)
- `API/appsettings.json` - Application configuration
- `API/appsettings.Development.json` - Development overrides

## Naming Conventions

- **Entities**: PascalCase, singular (Tenant, ApplicationUser)
- **Interfaces**: Prefixed with 'I' (IEmailService, ITenantResource)
- **DTOs**: Suffixed with purpose (LoginRequest, RegisterRequest, AuthResponse)
- **Services**: Suffixed with 'Service' (ClaimsService, TenantService)
- **Controllers**: Suffixed with 'Controller' (AuthenticationController)
- **Database tables**: snake_case (users, roles, user_tenants)
- **Constants**: PascalCase in static classes (Roles.SuperAdmin, Permissions.UsersCreate)

## Pragmatic Decisions

Authentication entities (ApplicationUser, ApplicationRole) live in Infrastructure rather than Domain because ASP.NET Core Identity is tightly coupled to Entity Framework. This pragmatic approach reduces complexity while maintaining clean separation for pure business entities.
