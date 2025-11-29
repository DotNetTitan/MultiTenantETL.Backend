# Using Local PostgreSQL Instead of Docker

## Configuration

The project is now configured to use your local PostgreSQL instance instead of Docker.

### Connection String

**Database:** MultiTenantETL  
**Username:** postgres  
**Password:** admin123  
**Port:** 5432  

### User Secrets (API)

```bash
cd src/MultiTenantETL.API
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=MultiTenantETL;Username=postgres;Password=admin123"
```

### Worker Configuration

Updated in `src/MultiTenantETL.Worker/appsettings.json`:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=MultiTenantETL;Username=postgres;Password=admin123"
  }
}
```

### Docker Compose

PostgreSQL service removed from `docker-compose.yml`. Only RabbitMQ remains:

```bash
docker-compose up -d  # Only starts RabbitMQ
```

## Database Setup

### Apply Migrations

```bash
cd src/MultiTenantETL.API
dotnet ef database update --project ../MultiTenantETL.Infrastructure
```

### Reset Database (if needed)

```bash
cd src/MultiTenantETL.API
dotnet ef database drop --project ../MultiTenantETL.Infrastructure --force
dotnet ef database update --project ../MultiTenantETL.Infrastructure
```

## Running the Application

### 1. Start RabbitMQ

```bash
docker-compose up -d
```

### 2. Start API

```bash
cd src/MultiTenantETL.API
dotnet run
```

### 3. Start Worker

```bash
cd src/MultiTenantETL.Worker
dotnet run
```

## Verification

Check that the database was created:

```sql
-- Connect to PostgreSQL
psql -U postgres -d MultiTenantETL

-- List tables
\dt

-- Should see:
-- audit_logs
-- connectors
-- execution_batches
-- execution_logs
-- pipeline_executions
-- pipelines
-- roles
-- tenants
-- transformations
-- users
-- etc.
```

## Migrations Applied

✅ All migrations applied successfully including:
- InitialCreate
- AddOpenIddictTables
- CreateOpenIddictTables
- AddAuditLogging
- AddConnectors
- AddTransformations
- AddPipelines
- AddPipelineExecutionSystem
- **ConvertStatusToEnum** (new - uses type-safe enums)

## Notes

- Local PostgreSQL must be running before starting the API or Worker
- RabbitMQ still runs in Docker (port 5672 for AMQP, 15672 for Management UI)
- Database seeding runs automatically in development mode when API starts
