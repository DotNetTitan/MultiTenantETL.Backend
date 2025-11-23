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

## Authentication & Authorization

- OpenIddict 7.2.0 (OAuth 2.0 & OpenID Connect)
- ASP.NET Core Identity (user/role management)
- Custom permission-based authorization handlers

## Key Libraries

- AspNetCoreRateLimit 5.0.0 (rate limiting)
- Azure.Communication.Email 1.1.0 (email service)
- BCrypt.Net-Next 4.0.3 (password hashing)
- OpenIddict.Quartz 7.2.0 (background token cleanup)

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
- Swagger UI available in development mode
- Database seeding runs automatically in development
