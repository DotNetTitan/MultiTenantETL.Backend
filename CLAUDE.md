# MultiTenant ETL Backend - Claude Code Instructions

> Essential project conventions and "always do this" rules for Claude Code

## Project Overview

**Enterprise-grade Multi-Tenant ETL platform** with .NET 8, PostgreSQL, OpenIddict OAuth 2.0, and 24 data connectors.

**Key Technologies:** ASP.NET Core 8 • EF Core 8 • PostgreSQL • RabbitMQ/Service Bus • OpenIddict 7.2 • .NET Aspire

**Critical Features:** Multi-tenancy with database isolation • Clean Architecture • Permission-based authorization • Async pipeline execution

## Architecture - STRICT RULES

### Layer Dependencies (NEVER VIOLATE)
```
API → Infrastructure → Application → Domain
```

**Rules:**
- Domain: NO dependencies on anything
- Application: Depends ONLY on Domain
- Infrastructure: Implements Application interfaces, depends on Domain + Application
- API/Worker: Orchestration layer, depends on all

### Core Patterns
- Repository: Implicit via EF Core DbContext
- Factory: `DataReaderFactory`, `DataWriterFactory`, `StorageClientFactory`
- Strategy: 24 connector implementations via common interfaces
- Message-Driven: RabbitMQ/Service Bus for async execution
- CQRS Lite: Service layer separates reads/writes

## Naming Conventions - ALWAYS FOLLOW

**Classes & Interfaces:**
- PascalCase: `Connector`, `Pipeline`, `PipelineExecution`
- Interface prefix: `IDataReader`, `IConnectorService`
- NO implementation suffix: `ConnectorService` (NOT `ConnectorServiceImpl`)
- Factory suffix: `DataReaderFactory`

**Fields & Properties:**
- Private fields: `_camelCase` with underscore (`_logger`, `_context`)
- Public properties: `PascalCase` (`TenantId`, `Name`, `IsActive`)
- Booleans: `Is`/`Has` prefix (`IsDeleted`, `HasCredentials`)

**Methods:**
- Async: ALWAYS use `Async` suffix: `CreateAsync`, `ExecutePipelineAsync`
- Factory: `Create` prefix: `CreateConnectionFactory`
- Tests: `{Method}_With{Condition}_Returns{Expected}`

**Files & Folders:**
- Plural for collections: `Services/`, `Controllers/`, `DataReaders/`
- One class per file (filename matches class name)
- Database tables: `snake_case` (`pipeline_executions`, `user_tenants`)

## Code Style - NON-NEGOTIABLE

- **Nullable Reference Types:** Enabled (`#nullable enable`)
- **Async/Await:** ALL database and I/O operations MUST be async
- **Required Properties:** Use `required` keyword for mandatory fields
- **XML Docs:** Comprehensive `///` comments on public APIs

## Multi-Tenancy - SECURITY CRITICAL

### ALWAYS Filter by TenantId
```csharp
// ✅ CORRECT
var data = await _context.Connectors
    .Where(c => c.TenantId == _currentUserService.CurrentTenantId)
    .ToListAsync();

// ❌ WRONG - Security breach!
var data = await _context.Connectors.ToListAsync();
```

### Entity Requirements
ALL tenant-scoped entities MUST:
1. Implement `ITenantResource` interface
2. Include `TenantId` property (Guid)
3. Include audit fields: `CreatedAt`, `UpdatedAt`, `CreatedBy`, `UpdatedBy`
4. Have EF Core configuration in `Infrastructure/Persistence/Configurations/`

**Template:**
```csharp
public class NewEntity : ITenantResource
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }  // REQUIRED
    public required string Name { get; set; }

    // Audit fields (REQUIRED)
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid CreatedBy { get; set; }
    public Guid? UpdatedBy { get; set; }

    public Tenant? Tenant { get; set; }
}
```

## Service Layer - MUST FOLLOW

**Service Structure:**
1. Define interface in `Application/{Domain}/I{Domain}Service.cs`
2. Implement in `Infrastructure/Services/{Domain}Service.cs`
3. ALWAYS filter by `CurrentTenantId` from `ICurrentUserService`
4. Return DTOs, NEVER entities
5. Use FluentValidation for input validation
6. Throw exceptions only for truly exceptional cases

**Example:**
```csharp
public async Task<ConnectorResponse> GetByIdAsync(Guid id)
{
    var tenantId = _currentUserService.CurrentTenantId; // ALWAYS
    var connector = await _context.Connectors
        .Where(c => c.TenantId == tenantId && c.Id == id)
        .FirstOrDefaultAsync();

    if (connector == null)
        throw new KeyNotFoundException($"Connector {id} not found");

    return MapToDto(connector); // Return DTO, not entity
}
```

## Controller Guidelines

**Requirements:**
- Use `[Authorize]` for protected endpoints
- Use `[RequirePermission("resource.action")]` for granular access
- Keep thin - delegate to services
- Return proper HTTP status codes

**Status Code Standards:**
- `200 OK` - GET/PUT success
- `201 Created` - POST success with Location header
- `204 No Content` - DELETE success
- `400 Bad Request` - Validation errors
- `401 Unauthorized` - Missing/invalid auth
- `403 Forbidden` - Insufficient permissions
- `404 Not Found` - Resource not found
- `409 Conflict` - Constraint violation
- `500 Internal Server Error` - Unhandled exceptions

## Security - NEVER COMPROMISE

**Critical Rules:**
1. **NEVER** hardcode credentials - use user secrets or env vars
2. **ALWAYS** validate input with FluentValidation
3. **ALWAYS** sanitize input (use `InputSanitizer`)
4. **NEVER** log sensitive data (passwords, tokens, credentials)
5. **ALWAYS** use parameterized queries (EF Core automatic)
6. Connector credentials: Encrypted (AES-GCM) in database

**Permission Format:** `{resource}.{action}` (e.g., `pipelines.create`, `connectors.read`)
- Supports wildcards: `tenants.*`, `*:read`, `*:*`
- Defined in `Domain/Constants/Permissions.cs`

## Common Commands

```bash
# Run with Aspire (RECOMMENDED - starts all services)
cd src/MultiTenantETL.AppHost && dotnet run

# Run API manually
cd src/MultiTenantETL.API && dotnet run
# API: https://localhost:7288, Swagger: /swagger

# Run Worker
cd src/MultiTenantETL.Worker && dotnet run

# Tests
dotnet test

# Migration
dotnet ef migrations add MigrationName \
  --project src/MultiTenantETL.Infrastructure \
  --startup-project src/MultiTenantETL.API

# User secrets (from API directory)
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;..."
```

## Configuration

**Provider Toggle (RabbitMQ vs Service Bus):**
```json
{ "Messaging": { "Provider": "RabbitMQ" } }  // or "ServiceBus"
```

**Hierarchy (priority):**
1. appsettings.json
2. appsettings.{Environment}.json
3. User Secrets (dev)
4. Environment Variables (prod)
5. Azure Key Vault (prod)

## Common Pitfalls - AVOID THESE

1. **Forgetting TenantId filter** → Always use `CurrentTenantId`
2. **Blocking calls** (`.Result`, `.Wait()`) → Use `await` everywhere
3. **Returning entities** → Always return DTOs from services
4. **Domain referencing Infrastructure** → Keep Domain pure
5. **Missing authorization** → Add `[Authorize]` and permissions
6. **Hardcoded secrets** → Use user secrets or env vars
7. **Connection leaks** → Use `await using` for DbContext/connections

## File Locations - Quick Reference

| What | Where |
|------|-------|
| Entities | `src/MultiTenantETL.Domain/Entities/` |
| Service Interfaces | `src/MultiTenantETL.Application/{Domain}/I{Name}Service.cs` |
| Service Implementations | `src/MultiTenantETL.Infrastructure/Services/{Name}Service.cs` |
| Controllers | `src/MultiTenantETL.API/Controllers/{Name}Controller.cs` |
| Data Readers | `src/MultiTenantETL.Infrastructure/DataReaders/` |
| Data Writers | `src/MultiTenantETL.Infrastructure/DataWriters/` |
| Transformations | `src/MultiTenantETL.Infrastructure/Transformations/` |
| DbContext | `src/MultiTenantETL.Infrastructure/Persistence/ApplicationDbContext.cs` |
| Migrations | `src/MultiTenantETL.Infrastructure/Persistence/Migrations/` |
| Constants | `src/MultiTenantETL.Domain/Constants/` |
| DTOs | `src/MultiTenantETL.Application/{Domain}/Dto/` |
| Validators | `src/MultiTenantETL.Application/{Domain}/Validators/` |
| Unit Tests | `tests/MultiTenantETL.UnitTests/` |
| Integration Tests | `tests/MultiTenantETL.IntegrationTests/` |

## Detailed Reference Guides

For detailed implementation guides, see `.claude/rules/`:
- `data-connectors.md` - Adding new readers/writers (24 connector types)
- `transformations.md` - Transformation engine and field mappings
- `pipeline-execution.md` - Pipeline orchestration flow
- `testing.md` - Unit and integration test patterns
- `messaging.md` - RabbitMQ/Service Bus configuration
- `configuration.md` - Complete configuration reference

## Additional Resources

- **Main README:** `/README.md` - Complete documentation
- **Copilot Instructions:** `/.github/copilot-instructions.md`
- **Auth Guide:** `/docs/auth/authentication-guide.md`
- **Azure Deployment:** `/docs/guides/AZURE-DEPLOYMENT-GUIDE.md`
- **Execution Engine:** `/docs/architecture/EXECUTION_ENGINE_IMPLEMENTATION.md`

---

**Remember:** Clean Architecture • Tenant Isolation • Async/Await • Security First
