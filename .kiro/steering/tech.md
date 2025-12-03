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

## Message Broker

- **RabbitMQ 3.13+** (pipeline execution queue)
- **RabbitMQ.Client 6.8.1** (.NET client)
- Durable queues with dead letter exchange
- Configurable prefetch count and retry attempts

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

## Data Connectors

### Database Libraries
- **Microsoft.Data.SqlClient** - SQL Server connectivity
- **Npgsql** - PostgreSQL connectivity  
- **MySqlConnector** - MySQL/MariaDB connectivity

### File & Storage Libraries
- **CsvHelper** - CSV reading/writing
- **System.Text.Json** - JSON/JSONL processing
- **SSH.NET 2024.1.0** - SFTP client library
- **FluentFTP** - FTP client library
- **AWSSDK.S3** - Amazon S3 storage
- **Azure.Storage.Blobs** - Azure Blob Storage

### API Libraries
- **System.Net.Http.Json** - REST API client

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

# Run Worker (from src/MultiTenantETL.Worker)
dotnet run

# Run with watch (auto-reload)
dotnet watch run
```

### Docker Compose

```bash
# Start infrastructure (PostgreSQL + RabbitMQ)
docker-compose up -d

# Stop infrastructure
docker-compose down

# View logs
docker-compose logs -f
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

# Run specific test project
dotnet test tests/MultiTenantETL.UnitTests
dotnet test tests/MultiTenantETL.IntegrationTests
```

## Development Environment

- Default API ports: HTTPS (7288), HTTP (5244)
- Default RabbitMQ ports: AMQP (5672), Management UI (15672)
- User secrets for sensitive configuration (UserSecretsId: 96149a75-7a4b-4db0-89c3-93fc63bf95e8)
- Swagger UI available in development mode at `/swagger`
- Database seeding runs automatically in development
- CORS configured for `http://localhost:5173` (Vue dev server)

## Configuration Files

### API Configuration (appsettings.json)
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=multitenant_etl;..."
  },
  "RabbitMq": {
    "HostName": "localhost",
    "Port": 5672,
    "UserName": "guest",
    "Password": "guest",
    "ExecutionQueueName": "pipeline-executions",
    "CancellationQueueName": "pipeline-cancellations",
    "PrefetchCount": 1,
    "MaxRetryAttempts": 5
  },
  "AzureCommunication": {
    "ConnectionString": "...",
    "SenderEmail": "noreply@yourdomain.com"
  }
}
```

## Security Configuration

- **Token Lifetimes**: Access tokens (15 min), Refresh tokens (7 days)
- **Account Lockout**: 5 failed attempts, 15-minute lockout duration
- **Password Requirements**: Min 8 chars, uppercase, lowercase, digit, special character
- **Rate Limiting**: Configured on authentication endpoints
- **Security Headers**: X-Content-Type-Options, X-Frame-Options, X-XSS-Protection, Referrer-Policy

## Infrastructure Services (Docker Compose)

- **PostgreSQL 16**: Database on port 5432
  - Default credentials: postgres/postgres
  - Default database: multitenant_etl
- **RabbitMQ 3.13**: Message broker on ports 5672 (AMQP), 15672 (Management UI)
  - Default credentials: guest/guest
  - Management UI: http://localhost:15672
