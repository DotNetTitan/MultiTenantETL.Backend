---
inclusion: always
---

# Project Structure

## Clean Architecture Layers

The project follows Clean Architecture principles with strict dependency rules:

```
MultiTenantETL/
├── src/
│   ├── MultiTenantETL.Domain/          # No dependencies - Pure business entities
│   ├── MultiTenantETL.Application/     # Depends on Domain only - Interfaces & DTOs
│   ├── MultiTenantETL.Infrastructure/  # Depends on Domain + Application - Implementations
│   ├── MultiTenantETL.API/             # Depends on all layers - Controllers & Configuration
│   └── MultiTenantETL.Worker/          # Background worker for pipeline execution
├── tests/
│   ├── MultiTenantETL.UnitTests/       # Unit tests
│   └── MultiTenantETL.IntegrationTests/ # Integration tests with test containers
└── docs/                                # Documentation
```

## Layer Responsibilities

### Domain Layer (MultiTenantETL.Domain)
- Pure business entities: `Tenant`, `Connector`, `Pipeline`, `Transformation`, `PipelineExecution`, `ExecutionBatch`, `ExecutionLogEntry`, `AuditLog`
- Domain interfaces: `ITenantResource` for tenant isolation authorization
- Enums: `AuthErrorCode`, `ExecutionStatus`, `BatchStatus`
- Constants: `Roles`, `Permissions`, `Policies`, `ClaimTypes`
- Value Objects: `ExecutionLog`
- No framework dependencies - pure C# business logic

### Application Layer (MultiTenantETL.Application)
- **Service Interfaces**: `IConnectorService`, `IPipelineService`, `ITransformationService`, `IExecutionService`, `IEmailService`, `ICurrentUserService`, `IClaimsService`, `ITenantService`, `IAuditService`
- **Data Access Interfaces**: `IDataReader`, `IDataWriter`, `IConnectionTester`, `ISchemaDetector`, `IFormatValidator`
- **Transformation Interfaces**: `ITransformationOrchestrator`, `ITransformationProcessor`
- **Messaging Interfaces**: `IMessagePublisher`, `ExecutionTask`
- **Orchestration Interfaces**: `IPipelineOrchestrator`
- **DTOs**: Request/response models for all entities (ConnectorDtos, PipelineDtos, TransformationDtos, ExecutionDtos, UserDtos, TenantDtos)
- **Common Models**: `ErrorResponse`, `ErrorDetail`
- Depends only on Domain

### Infrastructure Layer (MultiTenantETL.Infrastructure)
- **Persistence**: `ApplicationDbContext` with EF Core and PostgreSQL
- **Identity Models**: `ApplicationUser`, `ApplicationRole`, `UserTenant`
- **Data Readers**:
  - Database: `SqlServerDataReader`, `PostgreSqlDataReader`, `MySqlDataReader`
  - File: `CsvDataReader`, `JsonDataReader`, `JsonLinesDataReader`
  - Storage: `SftpDataReader`, `FtpDataReader`, `S3DataReader`, `AzureBlobDataReader`
  - API: `RestApiDataReader`
- **Data Writers**:
  - Database: `SqlServerDataWriter`, `PostgreSqlDataWriter`, `MySqlDataWriter`
  - File: `CsvDataWriter`, `JsonDataWriter`, `JsonLinesDataWriter`
  - Storage: `SftpDataWriter`, `FtpDataWriter`, `S3DataWriter`, `AzureBlobDataWriter`
  - API: `RestApiDataWriter`
- **Transformations**:
  - Core: `FilterTransformations`, `StringTransformations`, `ValueTransformations`
  - Processors: `FilterProcessor`, `MapProcessor`, `StringProcessor`, `ScriptProcessor`, `FieldTransformationProcessor`
  - Orchestration: `TransformationOrchestrator`
- **Messaging**: `RabbitMqPublisher`, `RabbitMqSettings`
- **Orchestration**: `PipelineOrchestrator`
- **Services**: `ConnectorService`, `PipelineService`, `TransformationService`, `ExecutionService`, `AuditService`, `EmailService`, `UserService`, `TenantService`, `ClaimsService`, `SchemaDetector`, `MetadataService`
- **Authorization Handlers**: `PermissionAuthorizationHandler`, `TenantResourceAuthorizationHandler`
- **Data Seeding**: `DbSeeder` for initial data (roles, permissions, admin, OAuth clients)
- **Database Migrations**: EF Core migrations
- **Security Utilities**: `InputSanitizer`
- **Configuration Classes**: `RabbitMqSettings`, `AzureCommunicationSettings`

### API Layer (MultiTenantETL.API)
- **Controllers**: 
  - `AuthenticationController` - OAuth 2.0 endpoints (handled by OpenIddict)
  - `AccountController` - Registration, password reset, email confirmation, logout, tenant switching
  - `UsersController` - User management CRUD operations
  - `TenantsController` - Tenant management CRUD operations
  - `ConnectorsController` - Connector CRUD with test and schema detection
  - `PipelinesController` - Pipeline CRUD with execute action
  - `TransformationsController` - Transformation CRUD
  - `ExecutionsController` - Execution management and monitoring
  - `AuditLogsController` - Audit log retrieval
  - `MetadataController` - Metadata for connector and transformation types
  - `FormatValidationController` - Data format validation
- **Middleware**: `SecurityHeadersMiddleware` for HTTP security headers
- **Configuration**: `Program.cs` with OpenIddict, DI, RabbitMQ, rate limiting, CORS, Swagger

### Worker Layer (MultiTenantETL.Worker)
- **Background Service**: `Worker` - Consumes pipeline execution tasks from RabbitMQ
- **Features**: 
  - Concurrent execution management with tracking
  - Cancellation handling via dedicated queue
  - Automatic retry with exponential backoff
  - Graceful shutdown with execution cleanup
- **Configuration**: `Program.cs` with DI, RabbitMQ, database context

## Key Folders

### Identity & Authentication
- `Domain/Constants/` - Roles (SuperAdmin, Admin, User), Permissions, Policies, ClaimTypes
- `Infrastructure/Identity/` - ApplicationUser, ApplicationRole, UserTenant, CurrentUserService
- `Infrastructure/Services/` - ClaimsService, TenantService, EmailService implementations
- `API/Controllers/` - AuthenticationController, AccountController

### Authorization
- `Domain/Constants/Permissions.cs` - Permission constants
- `Domain/Constants/Policies.cs` - Policy names
- `Infrastructure/Authorization/` - PermissionAuthorizationHandler, TenantResourceAuthorizationHandler

### Data Access
- `Application/Connectors/DataReaders/` - IDataReader interface and models
- `Application/Connectors/DataWriters/` - IDataWriter interface and models
- `Infrastructure/DataReaders/` - All data reader implementations
- `Infrastructure/DataWriters/` - All data writer implementations
- `Infrastructure/DataAccess/Readers/` - Factory pattern for readers
- `Infrastructure/DataAccess/Writers/` - Factory pattern for writers

### Transformations
- `Application/Transformations/` - ITransformationOrchestrator, ITransformationProcessor interfaces
- `Infrastructure/Transformations/Core/` - Core transformation functions
- `Infrastructure/Transformations/FieldProcessors/` - Field-level processors
- `Infrastructure/Transformations/Processors/` - Transformation type processors

### Messaging & Orchestration
- `Application/Messaging/` - IMessagePublisher, ExecutionTask
- `Application/Orchestration/` - IPipelineOrchestrator
- `Infrastructure/Messaging/` - RabbitMqPublisher
- `Infrastructure/Orchestration/` - PipelineOrchestrator

### Database
- `Infrastructure/Persistence/` - ApplicationDbContext with entity configurations
- `Infrastructure/Migrations/` - EF Core migrations
- `Infrastructure/Data/` - DbSeeder for initial data

### Configuration
- `Infrastructure/Configuration/` - Settings classes (RabbitMqSettings, AzureCommunicationSettings)
- `API/appsettings.json` - Application configuration
- `Worker/appsettings.json` - Worker configuration
- User secrets for sensitive data

## Naming Conventions

- **Entities**: PascalCase, singular (Tenant, Connector, Pipeline, Transformation, PipelineExecution)
- **Interfaces**: Prefixed with 'I' (IDataReader, IDataWriter, ITransformationProcessor)
- **DTOs**: Suffixed with purpose (ConnectorDto, CreateConnectorRequest, ConnectionTestResult)
- **Services**: Suffixed with 'Service' (ConnectorService, PipelineService, ExecutionService)
- **Controllers**: Suffixed with 'Controller' (ConnectorsController, PipelinesController)
- **Processors**: Suffixed with 'Processor' (FilterProcessor, MapProcessor, StringProcessor)
- **Database tables**: snake_case (users, connectors, pipelines, pipeline_executions)
- **Constants**: PascalCase in static classes (Roles.SuperAdmin, Permissions.Connectors.Read)
- **Authorization Handlers**: Suffixed with 'Handler' (PermissionAuthorizationHandler)

## Pragmatic Decisions

Authentication entities (ApplicationUser, ApplicationRole, UserTenant) live in Infrastructure rather than Domain because ASP.NET Core Identity is tightly coupled to Entity Framework. This pragmatic approach reduces complexity while maintaining clean separation for pure business entities like Tenant, Connector, and Pipeline.

## Key Design Patterns

- **Repository Pattern**: Abstracted through EF Core DbContext
- **Factory Pattern**: DataReaderFactory, DataWriterFactory for creating appropriate reader/writer instances
- **Strategy Pattern**: Different transformation processors for different transformation types
- **Service Layer**: Business logic in service implementations
- **Dependency Injection**: All services registered in Program.cs
- **Authorization Handlers**: Custom handlers for permission-based and resource-based authorization
- **Middleware Pipeline**: SecurityHeadersMiddleware for HTTP security headers
- **Seeding Pattern**: DbSeeder for initial data setup in development
- **Message Queue Pattern**: RabbitMQ for async pipeline execution
- **Worker Pattern**: Background service consuming execution tasks
