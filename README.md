# MultiTenant ETL - Enterprise ETL Platform

A production-ready, secure multi-tenant ASP.NET Core 8.0 platform designed for ETL (Extract, Transform, Load) operations with complete tenant isolation, OAuth 2.0/OpenID Connect authentication powered by OpenIddict, and a scalable pipeline execution engine with RabbitMQ message broker integration.

## 🚀 Features

### Multi-Tenancy
- **Complete Tenant Isolation**: Per-user tenant switching with automatic personal workspace creation on registration
- **Tenant Management**: Full CRUD operations with SuperAdmin/Admin controls
- **User-Tenant Relationships**: Users can belong to multiple tenants and switch between them seamlessly
- **Role-Based Access**: SuperAdmin, Admin, User roles within tenants

### ETL Pipeline Engine
- **Pipeline Management**: Create, configure, and manage ETL pipelines with source and destination connectors
- **Field Mappings**: Configure field-to-field mappings with inline transformations
- **Execution Tracking**: Real-time progress tracking with detailed logging
- **Batch Processing**: Memory-efficient streaming with configurable batch sizes (default 1,000 rows)
- **Scheduling Support**: Schedule configuration for automated pipeline runs

### Data Connectors
- **Database Connectors**: SQL Server, PostgreSQL, MySQL with connection pooling
- **File Connectors**: CSV, JSON, JSONL with local, FTP, SFTP, Azure Blob, and S3 storage
- **API Connectors**: REST API with authentication, headers, and pagination support
- **Connection Testing**: Validate connector configurations before pipeline execution
- **Schema Detection**: Auto-detect source schemas for field mapping assistance

### Transformation Engine
- **Filter Transformations**: Include/exclude rows based on conditions (equals, contains, regex, etc.)
- **Map Transformations**: Rename fields, apply value mappings
- **String Transformations**: Trim, case conversion, substring, replace, pad, concat
- **Script Transformations**: Custom JavaScript expressions for complex logic
- **Field-Level Processing**: Apply transformations to specific fields within a batch

### Message Broker Integration (RabbitMQ)
- **Asynchronous Execution**: Pipelines execute via background workers
- **Horizontal Scalability**: Run multiple worker instances for parallel processing
- **Durable Queues**: Persistent messages with dead letter exchange for failed tasks
- **Cancellation Support**: Graceful cancellation via dedicated queue
- **Retry Mechanism**: Configurable retry with exponential backoff

### Authentication & Authorization
- **OAuth 2.0 & OpenID Connect**: Powered by OpenIddict 7.2.0
  - Authorization Code + PKCE (recommended for SPAs) - RFC 7636 compliant
  - Password Grant (for API testing/machine-to-machine)
  - Refresh Token support with rotation
- **Token Management**: Short-lived access tokens (15 min), long-lived refresh tokens (7 days)
- **Permission-Based Authorization**: Fine-grained access control with custom handlers

### Security
- BCrypt password hashing
- Account lockout (5 failed attempts, 15-minute lockout)
- Rate limiting on authentication endpoints
- CORS configuration for frontend origins
- Security headers middleware
- Token revocation on password change and logout
- Input sanitization utilities
- Email enumeration prevention

### Clean Architecture
- Strict separation of concerns with Domain, Application, Infrastructure, API, and Worker layers

### .NET Aspire Support
- **Orchestration**: Single entry point to run all services with dependencies (PostgreSQL, RabbitMQ)
- **Service Discovery**: Built-in service discovery for inter-service communication
- **Health Checks**: Standardized health check endpoints (/health, /alive)
- **OpenTelemetry**: Distributed tracing and metrics out of the box
- **Resilience**: HTTP client resilience patterns (retries, circuit breakers)
- **Dashboard**: Aspire dashboard for monitoring all services during development

## 📋 Prerequisites

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [PostgreSQL 12+](https://www.postgresql.org/download/)
- [RabbitMQ 3.13+](https://www.rabbitmq.com/download.html) (for pipeline execution)
- [Docker](https://www.docker.com/get-started/) (required for Aspire orchestration)
- A code editor ([VS Code](https://code.visualstudio.com/), [Visual Studio](https://visualstudio.microsoft.com/), or [Rider](https://www.jetbrains.com/rider/))

## 🛠️ Setup Instructions

### Option A: Using .NET Aspire (Recommended)

The easiest way to run the complete application stack:

```bash
cd src/MultiTenantETL.AppHost
dotnet run
```

This automatically:
- Starts PostgreSQL with a data volume (managed by Aspire)
- Starts RabbitMQ with the management plugin (managed by Aspire)
- Starts the API service with automatic connection string injection
- Starts the Worker service with automatic connection string injection
- Opens the Aspire Dashboard for monitoring

> **Note:** When running with Aspire, you don't need to configure connection strings manually. Aspire automatically manages PostgreSQL and RabbitMQ containers and injects the correct connection strings into the API and Worker services.

The Aspire Dashboard will open in your browser, showing:
- All services and their health status
- Distributed traces across services
- Resource logs and metrics
- Service endpoints

### Option B: Manual Setup

#### 1. Clone the Repository

```bash
git clone <repository-url>
cd MultiTenantETL
```

#### 2. Start Infrastructure with Docker Compose

The easiest way to set up PostgreSQL and RabbitMQ:

```bash
docker-compose up -d
```

This starts:
- PostgreSQL on port 5432
- RabbitMQ on port 5672 (AMQP) and 15672 (Management UI)

#### 3. Configure Application Settings

Use **user secrets** for sensitive configuration (recommended for development):

```bash
cd src/MultiTenantETL.API

# Set database connection string
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=multitenant_etl;Username=postgres;Password=postgres"

# Set admin password for seeding
dotnet user-secrets set "Seeding:AdminPassword" "YOUR_SECURE_ADMIN_PASSWORD"

# Set OAuth client secret for testing
dotnet user-secrets set "Seeding:OAuthClientSecret" "YOUR_OAUTH_CLIENT_SECRET"

# Set Azure Communication Services (for email)
dotnet user-secrets set "AzureCommunication:ConnectionString" "your-azure-connection-string"
dotnet user-secrets set "AzureCommunication:SenderEmail" "noreply@yourdomain.com"
```

**User Secrets ID**: `96149a75-7a4b-4db0-89c3-93fc63bf95e8`

#### 4. Run Database Migrations

```bash
# From the project root
dotnet ef database update --project src/MultiTenantETL.Infrastructure --startup-project src/MultiTenantETL.API
```

This creates all necessary tables and seeds initial data (roles, permissions, admin user, OAuth clients).

#### 5. Run the Application

##### Start the API

```bash
cd src/MultiTenantETL.API
dotnet run
```

The API will be available at:
- **HTTPS**: `https://localhost:7288`
- **HTTP**: `http://localhost:5244`
- **Swagger UI**: `https://localhost:7288/swagger` (development only)

##### Start the Worker (for pipeline execution)

In a separate terminal:

```bash
cd src/MultiTenantETL.Worker
dotnet run
```

The Worker connects to RabbitMQ and processes pipeline execution tasks.

#### 6. Access RabbitMQ Management UI

Open browser: http://localhost:15672
- Username: `guest`
- Password: `guest`

Monitor queues, messages, and connections.

### 7. Default Admin Account

After seeding, log in with:
- **Email**: `admin@multitenant-etl.com`
- **Password**: The password you set in user secrets under `Seeding:AdminPassword`
- **Role**: SuperAdmin (full system access)

## 🏗️ Architecture

The project follows **Clean Architecture** principles with strict dependency rules:

```
MultiTenantETL/
├── src/
│   ├── MultiTenantETL.Domain/          # No dependencies - Pure business entities
│   ├── MultiTenantETL.Application/     # Depends on Domain only - Interfaces & DTOs
│   ├── MultiTenantETL.Infrastructure/  # Depends on Domain + Application - Implementations
│   ├── MultiTenantETL.API/             # Depends on all layers - Controllers & Configuration
│   ├── MultiTenantETL.Worker/          # Background worker for pipeline execution
│   ├── MultiTenantETL.AppHost/         # .NET Aspire orchestration host
│   └── MultiTenantETL.ServiceDefaults/ # Shared Aspire service defaults
├── tests/
│   ├── MultiTenantETL.UnitTests/       # Unit tests
│   └── MultiTenantETL.IntegrationTests/ # Integration tests
└── docs/                                # Documentation
```

### Layer Responsibilities

- **Domain Layer**: Pure business entities (Tenant, Connector, Pipeline, Transformation, PipelineExecution), domain interfaces (ITenantResource), enums, constants (Roles, Permissions, Policies)
- **Application Layer**: Service interfaces, DTOs, data reader/writer interfaces, transformation interfaces, orchestration abstractions
- **Infrastructure Layer**: ApplicationDbContext, data readers/writers, transformation processors, service implementations, authorization handlers, RabbitMQ integration, migrations
- **API Layer**: Controllers, middleware, OpenIddict configuration, rate limiting, CORS
- **Worker Layer**: Background service consuming pipeline execution tasks from RabbitMQ
- **AppHost Layer**: .NET Aspire orchestration for local development with automatic PostgreSQL and RabbitMQ container management
- **ServiceDefaults Layer**: Shared Aspire service defaults including OpenTelemetry, health checks, and HTTP resilience patterns

### Pipeline Execution Flow

```
API (ExecutionService)
  ↓ Publish ExecutionTask
RabbitMQ (pipeline-executions queue)
  ↓ Consume
Worker (Background Service)
  ↓ Execute
PipelineOrchestrator
  ↓ Stream batches
DataReader → TransformationOrchestrator → DataWriter
  ↓ Update
Database (execution_logs, execution_batches, pipeline_executions)
```

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

### Connector Management

| Endpoint | Method | Auth | Description |
|----------|--------|------|-------------|
| `/api/connectors` | GET | Yes | List all connectors for tenant |
| `/api/connectors/{id}` | GET | Yes | Get connector by ID |
| `/api/connectors` | POST | Yes | Create connector |
| `/api/connectors/{id}` | PUT | Yes | Update connector |
| `/api/connectors/{id}` | DELETE | Yes | Delete connector |
| `/api/connectors/{id}/test` | POST | Yes | Test connector connection |
| `/api/connectors/{id}/schema` | GET | Yes | Detect and get connector schema |

### Pipeline Management

| Endpoint | Method | Auth | Description |
|----------|--------|------|-------------|
| `/api/pipelines` | GET | Yes | List all pipelines for tenant |
| `/api/pipelines/{id}` | GET | Yes | Get pipeline by ID |
| `/api/pipelines` | POST | Yes | Create pipeline |
| `/api/pipelines/{id}` | PUT | Yes | Update pipeline |
| `/api/pipelines/{id}` | DELETE | Yes | Delete pipeline |
| `/api/pipelines/{id}/execute` | POST | Yes | Execute a pipeline |

### Execution Management

| Endpoint | Method | Auth | Description |
|----------|--------|------|-------------|
| `/api/executions` | GET | Yes | List executions (paginated, filtered) |
| `/api/executions/{id}` | GET | Yes | Get execution details with logs |
| `/api/executions/{id}/cancel` | POST | Yes | Cancel running execution |
| `/api/executions/stats` | GET | Yes | Get execution statistics |

### Metadata

| Endpoint | Method | Auth | Description |
|----------|--------|------|-------------|
| `/api/metadata/connector-types` | GET | Yes | Get available connector types |
| `/api/metadata/transformation-types` | GET | Yes | Get available transformation types |

## 🧪 Testing

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

For manual testing of the Authorization Code + PKCE flow, you can use cURL commands to simulate a SPA client.

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

#### Identity & Multi-Tenancy
- `users` - ASP.NET Identity users with multi-tenant support (ApplicationUser)
- `roles` - Application roles (ApplicationRole) with custom permissions
- `tenants` - Tenant organizations (Tenant entity in Domain layer)
- `user_tenants` - Many-to-many relationship between users and tenants (UserTenant)

#### OAuth & Authentication
- `OpenIddictApplications` - OAuth clients (multitenant-etl-spa, multitenant-etl-postman)
- `OpenIddictTokens` - Issued tokens (access tokens, refresh tokens)
- `OpenIddictAuthorizations` - Authorization grants
- `OpenIddictScopes` - Available OAuth scopes

#### ETL Domain
- `connectors` - Source and destination connector configurations
- `pipelines` - Pipeline definitions with field mappings
- `transformations` - Reusable transformation definitions
- `pipeline_executions` - Execution metadata and progress tracking
- `execution_batches` - Per-batch tracking for checkpointing
- `execution_logs` - Detailed execution logs (partitioned for retention)
- `audit_logs` - Audit trail for all operations

### Migrations

All migrations are located in `src/MultiTenantETL.Infrastructure/Migrations/` and managed by Entity Framework Core 8.0.

## ⚙️ Configuration

### RabbitMQ Settings (appsettings.json)

```json
{
  "RabbitMq": {
    "HostName": "localhost",
    "Port": 5672,
    "UserName": "guest",
    "Password": "guest",
    "VirtualHost": "/",
    "ExecutionQueueName": "pipeline-executions",
    "CancellationQueueName": "pipeline-cancellations",
    "DeadLetterExchange": "pipeline-dlx",
    "PrefetchCount": 1,
    "MaxRetryAttempts": 5
  }
}
```

### RabbitMQ Queues

- **pipeline-executions** - Durable queue for execution tasks
- **pipeline-cancellations** - Queue for cancellation requests  
- **pipeline-dlx** - Dead Letter Exchange for failed messages

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
- [ ] Configure production RabbitMQ connection
- [ ] Set up SSL certificates for token signing/encryption
- [ ] Configure CORS for your frontend origin
- [ ] Set up email service (Azure Communication Services, SendGrid, AWS SES)
- [ ] Configure logging and monitoring
- [ ] Set up health checks
- [ ] Review and update OAuth client redirect URIs
- [ ] Enable rate limiting on auth endpoints
- [ ] Configure backup strategy for database
- [ ] Set up worker service scaling (multiple instances for high load)

### Environment Variables

For production, use environment variables instead of appsettings:

```bash
ConnectionStrings__DefaultConnection="your-connection-string"
Seeding__AdminPassword="your-secure-password"
Seeding__OAuthClientSecret="your-oauth-secret"
RabbitMq__HostName="your-rabbitmq-host"
RabbitMq__UserName="your-rabbitmq-user"
RabbitMq__Password="your-rabbitmq-password"
```

### Scaling

#### Horizontal Scaling (Workers)

Run multiple worker instances for parallel pipeline processing:

```bash
# Terminal 1
dotnet run --project src/MultiTenantETL.Worker

# Terminal 2  
dotnet run --project src/MultiTenantETL.Worker

# Terminal 3
dotnet run --project src/MultiTenantETL.Worker
```

RabbitMQ distributes tasks across workers using round-robin.

#### Vertical Scaling (Concurrency)

Increase `PrefetchCount` in RabbitMQ settings for concurrent executions per worker:

```json
{
  "RabbitMq": {
    "PrefetchCount": 5
  }
}
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

#### Domain Layer (`src/MultiTenantETL.Domain`)
- **Entities**: `Tenant`, `Connector`, `Pipeline`, `Transformation`, `PipelineExecution`, `ExecutionBatch`, `ExecutionLogEntry`, `AuditLog`
- **Interfaces**: `ITenantResource` (for tenant isolation)
- **Constants**: `Roles`, `Permissions`, `Policies`, `ClaimTypes`
- **Enums**: `AuthErrorCode`, `ExecutionStatus`, `BatchStatus`
- **Value Objects**: `ExecutionLog`

#### Application Layer (`src/MultiTenantETL.Application`)
- **Service Interfaces**: `IConnectorService`, `IPipelineService`, `ITransformationService`, `IExecutionService`, `IEmailService`, `ICurrentUserService`, `IAuditService`
- **Data Access Interfaces**: `IDataReader`, `IDataWriter`, `IConnectionTester`, `ISchemaDetector`, `IFormatValidator`
- **Transformation Interfaces**: `ITransformationOrchestrator`, `ITransformationProcessor`
- **Messaging Interfaces**: `IMessagePublisher`
- **Orchestration Interfaces**: `IPipelineOrchestrator`
- **DTOs**: Request/response models for all entities

#### Infrastructure Layer (`src/MultiTenantETL.Infrastructure`)
- **Persistence**: `ApplicationDbContext` with EF Core and PostgreSQL
- **Identity**: `ApplicationUser`, `ApplicationRole`, `UserTenant` models
- **Data Readers**: `SqlServerDataReader`, `PostgreSqlDataReader`, `MySqlDataReader`, `CsvDataReader`, `JsonDataReader`, `JsonLinesDataReader`, `RestApiDataReader`, `SftpDataReader`, `FtpDataReader`, `S3DataReader`, `AzureBlobDataReader`
- **Data Writers**: `SqlServerDataWriter`, `PostgreSqlDataWriter`, `MySqlDataWriter`, `CsvDataWriter`, `JsonDataWriter`, `JsonLinesDataWriter`, `RestApiDataWriter`, `SftpDataWriter`, `FtpDataWriter`, `S3DataWriter`, `AzureBlobDataWriter`
- **Transformations**: `TransformationOrchestrator`, `FilterProcessor`, `MapProcessor`, `StringProcessor`, `ScriptProcessor`, `FieldTransformationProcessor`
- **Messaging**: `RabbitMqPublisher`
- **Orchestration**: `PipelineOrchestrator`
- **Services**: `ConnectorService`, `PipelineService`, `TransformationService`, `ExecutionService`, `AuditService`, `EmailService`, `UserService`, `TenantService`
- **Authorization**: `PermissionAuthorizationHandler`, `TenantResourceAuthorizationHandler`
- **Data**: `DbSeeder` for initial data
- **Migrations**: EF Core migrations

#### API Layer (`src/MultiTenantETL.API`)
- **Controllers**: `AuthenticationController`, `AccountController`, `UsersController`, `TenantsController`, `ConnectorsController`, `PipelinesController`, `TransformationsController`, `ExecutionsController`, `AuditLogsController`, `MetadataController`, `FormatValidationController`
- **Middleware**: `SecurityHeadersMiddleware` for HTTP security headers
- **Configuration**: `Program.cs` with OpenIddict, DI, RabbitMQ, rate limiting, CORS

#### Worker Layer (`src/MultiTenantETL.Worker`)
- **Background Service**: Consumes pipeline execution tasks from RabbitMQ
- **Features**: Concurrent execution management, cancellation handling, retry with exponential backoff, graceful shutdown

#### AppHost Layer (`src/MultiTenantETL.AppHost`)
- **.NET Aspire Orchestration**: Manages PostgreSQL and RabbitMQ containers for local development
- **Service Registration**: Registers API and Worker services with automatic connection string injection
- **Dashboard**: Provides Aspire Dashboard for monitoring all services

#### ServiceDefaults Layer (`src/MultiTenantETL.ServiceDefaults`)
- **OpenTelemetry**: Distributed tracing and metrics configuration
- **Health Checks**: Standardized health check endpoints
- **HTTP Resilience**: Retry policies and circuit breakers for HTTP clients
- **Service Discovery**: Built-in service discovery for inter-service communication

## 📖 Documentation

Detailed documentation is available in the [docs](./docs/) folder:

- **[Setup & Development Guides](./docs/guides/)** - Deployment, local setup, and configuration guides
  - [Azure Deployment Guide](./docs/guides/AZURE-DEPLOYMENT-GUIDE.md)
  - [Local PostgreSQL Setup](./docs/guides/SETUP-LOCAL-POSTGRES.md)
  - [Test Storage Servers](./docs/guides/TEST_STORAGE_SERVERS.md)

- **[Architecture Documentation](./docs/architecture/)** - System design and technical architecture
  - [Execution Engine](./docs/architecture/EXECUTION_ENGINE_IMPLEMENTATION.md)
  - [Transformation Engine](./docs/architecture/PHASE3_TRANSFORMATION_ENGINE.md)
  - [Audit Logging](./docs/architecture/AUDIT_LOGGING.md)

- **[Authentication & Authorization](./docs/auth/)** - Complete auth documentation
  - [Authentication Guide](./docs/auth/authentication-guide.md)
  - [Security Features](./docs/auth/5-security.md)
  - [Authorization](./docs/auth/6-authorization.md)

- **[Development Notes](./docs/development/)** - Historical implementation notes and phase summaries

## 📝 License

[Specify your license here]

## 🤝 Contributing

[Add contribution guidelines if open source]

## 📞 Support

For questions or issues, please [open an issue](link-to-issues) on GitHub.
