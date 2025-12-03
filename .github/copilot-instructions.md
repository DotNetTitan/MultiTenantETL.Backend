# GitHub Copilot Instructions for MultiTenant ETL

This document provides context and guidelines for GitHub Copilot when working with this codebase.

## Project Overview

MultiTenant ETL is an enterprise-grade ETL (Extract, Transform, Load) platform built with:
- **ASP.NET Core 8.0** Web API with Clean Architecture
- **PostgreSQL** with Entity Framework Core 8.0
- **RabbitMQ** for asynchronous pipeline execution
- **OpenIddict 7.2.0** for OAuth 2.0/OpenID Connect authentication

## Architecture Principles

### Clean Architecture Layers

1. **Domain Layer** (`src/MultiTenantETL.Domain/`) - Pure business entities with no external dependencies
2. **Application Layer** (`src/MultiTenantETL.Application/`) - Interfaces and DTOs, depends only on Domain
3. **Infrastructure Layer** (`src/MultiTenantETL.Infrastructure/`) - Implementations, depends on Domain and Application
4. **API Layer** (`src/MultiTenantETL.API/`) - Controllers and configuration
5. **Worker Layer** (`src/MultiTenantETL.Worker/`) - Background service for pipeline execution

### Key Patterns

- **Repository Pattern**: Abstracted through EF Core DbContext
- **Factory Pattern**: `DataReaderFactory`, `DataWriterFactory` for creating connectors
- **Strategy Pattern**: Different processors for different transformation types
- **Message Queue Pattern**: RabbitMQ for async pipeline execution

## Coding Conventions

### Naming

- **Entities**: PascalCase, singular (e.g., `Connector`, `Pipeline`, `Transformation`)
- **Interfaces**: Prefix with 'I' (e.g., `IDataReader`, `ITransformationProcessor`)
- **DTOs**: Suffix with purpose (e.g., `CreateConnectorRequest`, `ConnectorDto`)
- **Services**: Suffix with 'Service' (e.g., `ConnectorService`, `PipelineService`)
- **Controllers**: Suffix with 'Controller' (e.g., `ConnectorsController`)
- **Database tables**: snake_case (e.g., `pipeline_executions`, `execution_logs`)

### Entity Guidelines

All tenant-scoped entities must:
1. Implement `ITenantResource` interface
2. Include `TenantId` property for tenant isolation
3. Include audit fields: `CreatedAt`, `UpdatedAt`, `CreatedBy`, `UpdatedBy`

Example:
```csharp
public class NewEntity : ITenantResource
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public required string Name { get; set; }
    
    // Audit fields
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid CreatedBy { get; set; }
    public Guid? UpdatedBy { get; set; }

    // Navigation
    public Tenant? Tenant { get; set; }
}
```

### Service Guidelines

Services should:
1. Define interface in Application layer
2. Implement in Infrastructure layer
3. Always filter by `TenantId` for tenant isolation
4. Use async/await for database operations
5. Return DTOs, not entities

### Controller Guidelines

Controllers should:
1. Use `[Authorize]` attribute for protected endpoints
2. Use permission-based authorization where needed
3. Return appropriate HTTP status codes
4. Use dependency injection for services

## Data Connectors

### Supported Types

**Database**: SQL Server, PostgreSQL, MySQL
**File**: CSV, JSON, JSONL (with Local, FTP, SFTP, S3, Azure Blob storage)
**API**: REST API with authentication support

### Adding a New Data Reader

1. Create class in `Infrastructure/DataReaders/` implementing `IDataReader`
2. Register in `DataReaderFactory`
3. Add connection tester in `Infrastructure/Services/ConnectionTesting/`

### Adding a New Data Writer

1. Create class in `Infrastructure/DataWriters/` implementing `IDataWriter`
2. Register in `DataWriterFactory`

## Transformations

### Supported Types

- **Filter**: Include/exclude rows based on conditions
- **Map**: Rename fields, apply value mappings
- **String**: Trim, case conversion, substring, replace, pad, concat
- **Script**: Custom JavaScript expressions

### Adding a New Transformation

1. Create processor in `Infrastructure/Transformations/Processors/` implementing `ITransformationProcessor`
2. Register in `TransformationOrchestrator`
3. Add configuration model in `Application/Transformations/Models/`

## Message Broker

### RabbitMQ Queues

- `pipeline-executions` - Main execution queue
- `pipeline-cancellations` - Cancellation requests
- `pipeline-dlx` - Dead letter exchange for failed messages

### Publishing Messages

Use `IMessagePublisher` interface:
```csharp
await _messagePublisher.PublishExecutionTaskAsync(new ExecutionTask { ... });
```

## Testing

### Unit Tests

Located in `tests/MultiTenantETL.UnitTests/`

### Integration Tests

Located in `tests/MultiTenantETL.IntegrationTests/`
- Use test containers for database and RabbitMQ
- Test data reader/writer implementations

## Common Commands

```bash
# Build
dotnet build

# Run API
cd src/MultiTenantETL.API && dotnet run

# Run Worker
cd src/MultiTenantETL.Worker && dotnet run

# Run tests
dotnet test

# Create migration
dotnet ef migrations add MigrationName --project src/MultiTenantETL.Infrastructure --startup-project src/MultiTenantETL.API

# Apply migrations
dotnet ef database update --project src/MultiTenantETL.Infrastructure --startup-project src/MultiTenantETL.API
```

## Security Considerations

1. **Tenant Isolation**: Always filter queries by TenantId
2. **Authorization**: Use permission-based policies for sensitive operations
3. **Secrets**: Never hardcode credentials; use user secrets or environment variables
4. **Input Validation**: Validate all user inputs
5. **SQL Injection**: Use parameterized queries (EF Core handles this)

## Configuration

### Required Settings

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "PostgreSQL connection string"
  },
  "RabbitMq": {
    "HostName": "localhost",
    "Port": 5672,
    "UserName": "guest",
    "Password": "guest"
  },
  "AzureCommunication": {
    "ConnectionString": "Azure Communication Services connection",
    "SenderEmail": "noreply@yourdomain.com"
  }
}
```

## Dependencies

When adding new NuGet packages:
1. Check for security vulnerabilities
2. Prefer packages with active maintenance
3. Add to appropriate project (Domain should have no dependencies)
4. Document the purpose in the PR

## Additional Resources

- Main README: `/README.md`
- Phase 4 Implementation: `/README-PHASE4.md`
- SFTP Implementation: `/SFTP_IMPLEMENTATION.md`
- Steering docs: `/.kiro/steering/`
