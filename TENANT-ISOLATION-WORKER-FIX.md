# Tenant Isolation for Background Workers - Implementation Summary

## Problem Identified

The AI review correctly identified a critical gap in tenant isolation: while HTTP requests properly enforce tenant context through claims-based authentication, **background worker processes had no tenant context**, potentially allowing cross-tenant data leakage.

### Root Cause
- `CurrentUserService` relied on `IHttpContextAccessor` which returns `null` in non-HTTP contexts
- `ApplicationDbContext` had no global query filters for tenant isolation
- Worker jobs would get `Guid.Empty` as tenant ID, bypassing tenant isolation

## Solution Implemented

Introduced `ITenantProvider` - a scoped service that provides tenant context for both HTTP and non-HTTP scenarios.

### Architecture

```
HTTP Request Flow:
User Request → Claims → CurrentUserService → ITenantProvider (fallback) → DbContext Filters

Worker Job Flow:
Message Queue → Worker → Set ITenantProvider.TenantId → Scoped Services → DbContext Filters
```

## Changes Made

### 1. New Interface: `ITenantProvider`
**File:** `src/MultiTenantETL.Application/Common/Interfaces/ITenantProvider.cs`

```csharp
public interface ITenantProvider
{
    Guid? TenantId { get; set; }
    string? CorrelationId { get; set; }
}
```

### 2. Implementation: `TenantProvider`
**File:** `src/MultiTenantETL.Infrastructure/Identity/TenantProvider.cs`

- Scoped lifetime prevents tenant bleed between concurrent jobs
- Simple property-based storage for tenant context

### 3. Updated `CurrentUserService`
**File:** `src/MultiTenantETL.Infrastructure/Identity/CurrentUserService.cs`

- Now accepts `ITenantProvider` in constructor
- `GetTenantId()` falls back to `ITenantProvider.TenantId` when `HttpContext` is null
- Works seamlessly in both HTTP and worker contexts

### 4. Updated `ApplicationDbContext`
**File:** `src/MultiTenantETL.Infrastructure/Persistence/ApplicationDbContext.cs`

**Key additions:**
- Accepts `ITenantProvider` in constructor
- `ApplyTenantQueryFilters()` - Automatically applies filters to all `ITenantResource` entities
- `SetTenantQueryFilter<TEntity>()` - Generic method that creates EF Core query filters

**Result:** All queries automatically filter by `TenantId` from the scoped provider

### 5. Updated Worker
**File:** `src/MultiTenantETL.Worker/Worker.cs`

**Critical changes in `HandleExecutionTask()`:**
1. **Validates** `task.TenantId` is present (rejects if empty)
2. **Creates scope** per job: `using var scope = _serviceProvider.CreateScope()`
3. **Sets tenant context**: `tenantProvider.TenantId = task.TenantId`
4. **Adds structured logging** with TenantId and CorrelationId
5. **Resolves services** from scope (ensuring they use the scoped tenant context)

### 6. DI Registration

**API:** `src/MultiTenantETL.API/Program.cs`
```csharp
builder.Services.AddScoped<ITenantProvider, TenantProvider>();
```

**Worker:** `src/MultiTenantETL.Worker/Program.cs`
```csharp
builder.Services.AddScoped<ITenantProvider, TenantProvider>();
```

### 7. Test Fixes

Updated all unit tests to mock `ITenantProvider` when creating `ApplicationDbContext`:
- `ConnectorServiceTests.cs`
- `TransformationServiceTests.cs`
- `ExecutionServiceTests.cs`
- `PipelineServiceTests.cs`
- `ClaimsServiceTests.cs`
- `TenantServiceTests.cs`

## Security Benefits

### Before
❌ Worker jobs had no tenant context  
❌ EF Core queries returned data from all tenants  
❌ Risk of cross-tenant data leakage  
❌ No audit trail for tenant in worker logs  

### After
✅ Explicit tenant validation on every job  
✅ EF Core automatically filters all queries by tenant  
✅ Scoped lifetime prevents concurrent job interference  
✅ Structured logging includes TenantId and CorrelationId  
✅ Works identically in HTTP and worker contexts  

## How It Works

### HTTP Request Example
```csharp
// 1. User authenticates, claims include TenantId
// 2. CurrentUserService reads from HttpContext claims
// 3. If no HttpContext, falls back to ITenantProvider
// 4. DbContext uses ITenantProvider for query filters
```

### Worker Job Example
```csharp
// 1. Message arrives with ExecutionTask containing TenantId
// 2. Worker validates TenantId is present
// 3. Creates new scope for job
// 4. Sets ITenantProvider.TenantId = task.TenantId
// 5. All services in scope use this tenant context
// 6. DbContext query filters automatically apply
// 7. Scope disposed, tenant context cleared
```

## Testing Recommendations

1. **Unit Tests:** Verify `ITenantProvider` is set correctly in worker scopes
2. **Integration Tests:** Run concurrent jobs for different tenants, verify no data bleed
3. **Load Tests:** Ensure scoped lifetime handles high concurrency
4. **Audit Tests:** Verify TenantId appears in all worker logs

## Migration Notes

- **No database migration required** - this is purely application-level
- **Existing data unaffected** - only query behavior changes
- **Backward compatible** - HTTP flows work exactly as before
- **Message format unchanged** - `ExecutionTask` already had `TenantId`

## Future Enhancements

1. **DB-per-tenant support:** Extend `ITenantProvider` to include connection string selection
2. **AsyncLocal backing:** For scenarios requiring ambient context across async boundaries
3. **Tenant validation service:** Centralized tenant existence/active status checks
4. **Metrics:** Track tenant context misses or validation failures

## Tests Created

### Unit Tests
**Location:** `tests/MultiTenantETL.UnitTests/Identity/`

1. **TenantProviderTests.cs** - 7 tests
   - Default values are null
   - Can set and get TenantId
   - Can set and get CorrelationId
   - Can set both properties
   - Can clear values
   - Multiple instances are independent

2. **CurrentUserServiceTenantProviderTests.cs** - 11 tests
   - Falls back to TenantProvider when no HttpContext
   - Falls back when HttpContext has no claim
   - Falls back when claim value is invalid
   - Returns Guid.Empty when TenantProvider is null
   - All other methods return safe defaults without HttpContext

**Test Results:** ✅ All 18 tests passed

### Integration Tests (Testcontainers)
**Location:** `tests/MultiTenantETL.IntegrationTests/TenantIsolation/`

**WorkerTenantIsolationTests.cs** - Uses real PostgreSQL via Testcontainers
- DbContext with TenantProvider filters queries by tenant
- Concurrent scopes with different tenants don't interfere
- TenantProvider not set returns no data

These tests use Testcontainers to spin up a real PostgreSQL database, ensuring the EF Core global query filters work correctly in production-like scenarios. This validates that tenant isolation works properly with actual database connections, not just in-memory simulations.

## Verification

Build successful:
```bash
dotnet build
# Build succeeded in 2.2s
```

All projects compile without errors:
- ✅ MultiTenantETL.API
- ✅ MultiTenantETL.Worker
- ✅ MultiTenantETL.Infrastructure
- ✅ MultiTenantETL.UnitTests (18/18 tests passed)
- ✅ MultiTenantETL.IntegrationTests
