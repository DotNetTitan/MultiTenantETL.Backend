# Database Implementation Guide

**Database:** PostgreSQL  
**Version:** PostgreSQL 14 or higher recommended  
**Last Updated:** 2025-11-19

---

## Overview

This guide provides PostgreSQL-specific implementation details for the Multi-Tenant ETL Platform API.

---

## ID Fields - UUID Strategy

### Why UUIDs?

For this multi-tenant platform, we use **UUIDs** for all primary keys:

1. **Security**: Non-sequential IDs prevent ID enumeration attacks
2. **Multi-tenancy**: Globally unique across all tenants
3. **Distribution**: Ready for distributed systems/microservices
4. **No collisions**: Safe for client-side generation if needed
5. **PostgreSQL native**: Built-in `uuid` type with good performance

### PostgreSQL UUID Setup

**Enable UUID extension:**
```sql
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
-- OR use PostgreSQL 13+ built-in:
CREATE EXTENSION IF NOT EXISTS "pgcrypto";
```

**Table definition pattern:**
```sql
CREATE TABLE pipelines (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id UUID NOT NULL REFERENCES tenants(id),
    name VARCHAR(100) NOT NULL,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    -- ... other fields
);
```

### API Representation

**JSON Format:**
```json
{
  "id": "550e8400-e29b-41d4-a716-446655440000"
}
```

UUIDs are represented as strings in JSON (standard UUID string format).

---

## Table Schemas

### Tenants Table

```sql
CREATE TABLE tenants (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name VARCHAR(100) NOT NULL,
    identifier VARCHAR(50) NOT NULL UNIQUE,
    description TEXT,
    contact_name VARCHAR(100),
    contact_email VARCHAR(255),
    is_active BOOLEAN DEFAULT true,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX idx_tenants_identifier ON tenants(identifier);
CREATE INDEX idx_tenants_is_active ON tenants(is_active);
```

---

### Users Table

```sql
CREATE TABLE users (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id UUID NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    first_name VARCHAR(100) NOT NULL,
    last_name VARCHAR(100) NOT NULL,
    email VARCHAR(255) NOT NULL,
    password_hash VARCHAR(255) NOT NULL,
    role VARCHAR(20) NOT NULL CHECK (role IN ('Admin', 'Manager', 'User')),
    is_active BOOLEAN DEFAULT true,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    UNIQUE(tenant_id, email)
);

CREATE INDEX idx_users_tenant_id ON users(tenant_id);
CREATE INDEX idx_users_email ON users(email);
CREATE INDEX idx_users_is_active ON users(is_active);
```

---

## Lookup Tables (Enums & Reference Data)

All enum-like fields use lookup tables for internationalization, dynamic management, and data integrity.

### Connector Types and Providers (Lookup Tables)

```sql
-- Fixed connector types (rarely changes)
CREATE TABLE connector_types (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    code VARCHAR(20) UNIQUE NOT NULL,  -- 'database', 'file', 'api'
    display_name VARCHAR(50) NOT NULL,  -- 'Database', 'File', 'API'
    icon VARCHAR(50),
    is_active BOOLEAN DEFAULT true,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP
);

-- Dynamic connector providers (can be added via API)
CREATE TABLE connector_providers (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    type_code VARCHAR(20) NOT NULL REFERENCES connector_types(code) ON DELETE CASCADE,
    code VARCHAR(50) NOT NULL,  -- 'sqlserver', 'postgresql', 's3', 'rest'
    display_name VARCHAR(100) NOT NULL,  -- 'SQL Server', 'PostgreSQL', 'S3', 'REST'
    is_active BOOLEAN DEFAULT true,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    UNIQUE(type_code, code),
    UNIQUE(type_code, display_name)
);

CREATE INDEX idx_connector_providers_type_code ON connector_providers(type_code);
CREATE INDEX idx_connector_providers_is_active ON connector_providers(is_active);

-- Seed connector types (these rarely change)
INSERT INTO connector_types (code, display_name, icon) VALUES
    ('database', 'Database', 'mdi-database'),
    ('file', 'File', 'mdi-file-document'),
    ('api', 'API', 'mdi-api');

-- Seed common providers (more can be added via API)
INSERT INTO connector_providers (type_code, code, display_name) VALUES
    -- Database providers
    ('database', 'sqlserver', 'SQL Server'),
    ('database', 'postgresql', 'PostgreSQL'),
    ('database', 'mysql', 'MySQL'),
    ('database', 'oracle', 'Oracle'),
    ('database', 'sqlite', 'SQLite'),
    -- File providers
    ('file', 'local', 'Local'),
    ('file', 'ftp', 'FTP'),
    ('file', 'sftp', 'SFTP'),
    ('file', 's3', 'S3'),
    ('file', 'azure_blob', 'Azure Blob'),
    ('file', 'gcs', 'Google Cloud Storage'),
    -- API providers
    ('api', 'rest', 'REST'),
    ('api', 'graphql', 'GraphQL'),
    ('api', 'soap', 'SOAP');
```

---

### Transformation Types

```sql
CREATE TABLE transformation_types (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    code VARCHAR(50) UNIQUE NOT NULL,  -- 'filter', 'map', 'trim', 'case_convert', 'substring', 'replace', 'script'
    display_name VARCHAR(100) NOT NULL,  -- 'Filter', 'Map', 'Trim', 'Case Convert', 'Substring', 'Replace', 'Script'
    icon VARCHAR(50),
    category VARCHAR(50),  -- 'data_quality', 'transformation', 'text', 'custom'
    is_active BOOLEAN DEFAULT true,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP
);

INSERT INTO transformation_types (code, display_name, icon, category) VALUES
    ('filter', 'Filter', 'mdi-filter', 'data_quality'),
    ('map', 'Map', 'mdi-map', 'transformation'),
    ('trim', 'Trim', 'mdi-content-cut', 'text'),
    ('case_convert', 'Case Convert', 'mdi-format-letter-case', 'text'),
    ('substring', 'Substring', 'mdi-text-box-outline', 'text'),
    ('replace', 'Replace', 'mdi-find-replace', 'text'),
    ('script', 'Script', 'mdi-code-braces', 'custom');
```

---

### Statuses

```sql
-- Pipeline statuses
CREATE TABLE pipeline_statuses (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    code VARCHAR(20) UNIQUE NOT NULL,  -- 'idle', 'running', 'failed'
    display_name VARCHAR(50) NOT NULL,  -- 'Idle', 'Running', 'Failed'
    color VARCHAR(20),  -- 'success', 'warning', 'error'
    icon VARCHAR(50),
    is_active BOOLEAN DEFAULT true
);

INSERT INTO pipeline_statuses (code, display_name, color, icon) VALUES
    ('idle', 'Idle', 'default', 'mdi-sleep'),
    ('running', 'Running', 'primary', 'mdi-play-circle'),
    ('failed', 'Failed', 'error', 'mdi-alert-circle');

-- Execution statuses
CREATE TABLE execution_statuses (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    code VARCHAR(20) UNIQUE NOT NULL,  -- 'running', 'completed', 'failed'
    display_name VARCHAR(50) NOT NULL,  -- 'Running', 'Completed', 'Failed'
    color VARCHAR(20),
    icon VARCHAR(50),
    is_active BOOLEAN DEFAULT true
);

INSERT INTO execution_statuses (code, display_name, color, icon) VALUES
    ('running', 'Running', 'primary', 'mdi-play-circle'),
    ('completed', 'Completed', 'success', 'mdi-check-circle'),
    ('failed', 'Failed', 'error', 'mdi-alert-circle');
```

---

### User Roles

```sql
CREATE TABLE user_roles (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    code VARCHAR(20) UNIQUE NOT NULL,  -- 'admin', 'manager', 'user'
    display_name VARCHAR(50) NOT NULL,  -- 'Admin', 'Manager', 'User'
    permissions JSONB,  -- Array of permission codes
    is_active BOOLEAN DEFAULT true,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP
);

INSERT INTO user_roles (code, display_name, permissions) VALUES
    ('admin', 'Admin', '["*"]'),
    ('manager', 'Manager', '["pipelines.*", "connectors.*", "transformations.*", "executions.view"]'),
    ('user', 'User', '["pipelines.view", "executions.view", "dashboard.view"]');
```

---

### Schedule Frequencies

```sql
CREATE TABLE schedule_frequencies (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    code VARCHAR(20) UNIQUE NOT NULL,  -- 'daily', 'weekly', 'monthly', 'custom'
    display_name VARCHAR(50) NOT NULL,  -- 'Daily', 'Weekly', 'Monthly', 'Custom'
    icon VARCHAR(50),
    is_active BOOLEAN DEFAULT true
);

INSERT INTO schedule_frequencies (code, display_name, icon) VALUES
    ('daily', 'Daily', 'mdi-calendar-today'),
    ('weekly', 'Weekly', 'mdi-calendar-week'),
    ('monthly', 'Monthly', 'mdi-calendar-month'),
    ('custom', 'Custom', 'mdi-cog');
```

---

### File Formats

```sql
CREATE TABLE file_formats (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    code VARCHAR(20) UNIQUE NOT NULL,  -- 'csv', 'json', 'excel', 'xml', 'parquet'
    display_name VARCHAR(50) NOT NULL,  -- 'CSV', 'JSON', 'Excel', 'XML', 'Parquet'
    extension VARCHAR(10),  -- '.csv', '.json', '.xlsx', '.xml', '.parquet'
    mime_type VARCHAR(100),  -- 'text/csv', 'application/json', etc.
    is_active BOOLEAN DEFAULT true
);

INSERT INTO file_formats (code, display_name, extension, mime_type) VALUES
    ('csv', 'CSV', '.csv', 'text/csv'),
    ('json', 'JSON', '.json', 'application/json'),
    ('excel', 'Excel', '.xlsx', 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet'),
    ('xml', 'XML', '.xml', 'application/xml'),
    ('parquet', 'Parquet', '.parquet', 'application/octet-stream');
```

---

### Auth Types

```sql
CREATE TABLE auth_types (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    code VARCHAR(20) UNIQUE NOT NULL,  -- 'none', 'basic', 'bearer', 'oauth2', 'api_key'
    display_name VARCHAR(50) NOT NULL,  -- 'None', 'Basic', 'Bearer', 'OAuth2', 'API Key'
    requires_credentials BOOLEAN DEFAULT true,
    is_active BOOLEAN DEFAULT true
);

INSERT INTO auth_types (code, display_name, requires_credentials) VALUES
    ('none', 'None', false),
    ('basic', 'Basic', true),
    ('bearer', 'Bearer', true),
    ('oauth2', 'OAuth2', true),
    ('api_key', 'API Key', true);
```

---

### Database Write Operations

```sql
CREATE TABLE write_operations (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    code VARCHAR(20) UNIQUE NOT NULL,  -- 'insert', 'update', 'upsert', 'bulk_insert'
    display_name VARCHAR(50) NOT NULL,  -- 'INSERT', 'UPDATE', 'UPSERT', 'BULK_INSERT'
    requires_primary_key BOOLEAN DEFAULT false,
    description TEXT,
    is_active BOOLEAN DEFAULT true
);

INSERT INTO write_operations (code, display_name, requires_primary_key, description) VALUES
    ('insert', 'INSERT', false, 'Insert new records only'),
    ('update', 'UPDATE', true, 'Update existing records'),
    ('upsert', 'UPSERT', true, 'Insert or update records'),
    ('bulk_insert', 'BULK_INSERT', false, 'Fast bulk insert with minimal validation');
```

---

### Data Types

```sql
CREATE TABLE data_types (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    code VARCHAR(20) UNIQUE NOT NULL,  -- 'string', 'integer', 'decimal', 'date', etc.
    display_name VARCHAR(50) NOT NULL,  -- 'String', 'Integer', 'Decimal', 'Date', etc.
    icon VARCHAR(50),
    category VARCHAR(50),  -- 'text', 'numeric', 'temporal', 'other'
    is_active BOOLEAN DEFAULT true
);

INSERT INTO data_types (code, display_name, icon, category) VALUES
    ('string', 'String', 'mdi-format-text', 'text'),
    ('integer', 'Integer', 'mdi-numeric', 'numeric'),
    ('big_integer', 'Big Integer', 'mdi-numeric', 'numeric'),
    ('decimal', 'Decimal', 'mdi-decimal', 'numeric'),
    ('boolean', 'Boolean', 'mdi-checkbox-marked', 'other'),
    ('date', 'Date', 'mdi-calendar', 'temporal'),
    ('date_time', 'DateTime', 'mdi-calendar-clock', 'temporal'),
    ('timestamp', 'Timestamp', 'mdi-clock-outline', 'temporal'),
    ('json', 'JSON', 'mdi-code-json', 'other'),
    ('text_long', 'Text (Long)', 'mdi-text-long', 'text');
```

---

### HTTP Methods (for API connectors)

```sql
CREATE TABLE http_methods (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    code VARCHAR(10) UNIQUE NOT NULL,  -- 'get', 'post', 'put', 'patch', 'delete'
    display_name VARCHAR(10) NOT NULL,  -- 'GET', 'POST', 'PUT', 'PATCH', 'DELETE'
    color VARCHAR(20),  -- 'success', 'primary', 'warning', 'info', 'error'
    is_safe BOOLEAN DEFAULT false,  -- Safe methods don't modify data
    is_idempotent BOOLEAN DEFAULT false,  -- Idempotent methods have same effect when called multiple times
    is_active BOOLEAN DEFAULT true
);

INSERT INTO http_methods (code, display_name, color, is_safe, is_idempotent) VALUES
    ('get', 'GET', 'success', true, true),
    ('post', 'POST', 'primary', false, false),
    ('put', 'PUT', 'warning', false, true),
    ('patch', 'PATCH', 'info', false, false),
    ('delete', 'DELETE', 'error', false, true);
```

---

### Connector Directions

```sql
CREATE TABLE connector_directions (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    code VARCHAR(20) UNIQUE NOT NULL,  -- 'source', 'destination', 'both'
    display_name VARCHAR(50) NOT NULL,  -- 'Source', 'Destination', 'Both'
    icon VARCHAR(50),
    is_active BOOLEAN DEFAULT true
);

INSERT INTO connector_directions (code, display_name, icon) VALUES
    ('source', 'Source', 'mdi-export'),
    ('destination', 'Destination', 'mdi-import'),
    ('both', 'Both', 'mdi-swap-horizontal');
```

---

## Core Tables

Now the main tables reference these lookup tables via codes:

### Tenants Table

(Same as before - no changes needed)

---

### Users Table

```sql
CREATE TABLE users (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id UUID NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    first_name VARCHAR(100) NOT NULL,
    last_name VARCHAR(100) NOT NULL,
    email VARCHAR(255) NOT NULL,
    password_hash VARCHAR(255) NOT NULL,
    role_code VARCHAR(20) NOT NULL REFERENCES user_roles(code),
    is_active BOOLEAN DEFAULT true,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    UNIQUE(tenant_id, email)
);

CREATE INDEX idx_users_tenant_id ON users(tenant_id);
CREATE INDEX idx_users_email ON users(email);
CREATE INDEX idx_users_role_code ON users(role_code);
CREATE INDEX idx_users_is_active ON users(is_active);
```

**Note:** Uses `role_code` to reference `user_roles` lookup table.

---

### Connectors Table

```sql
CREATE TABLE connectors (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id UUID NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    name VARCHAR(100) NOT NULL,
    description TEXT,
    type_code VARCHAR(20) NOT NULL REFERENCES connector_types(code),
    provider_code VARCHAR(50) NOT NULL,
    direction_code VARCHAR(20) NOT NULL REFERENCES connector_directions(code),
    config JSONB NOT NULL,
    schema JSONB,
    requires_credentials BOOLEAN DEFAULT true,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    UNIQUE(tenant_id, name),
    FOREIGN KEY (type_code, provider_code) REFERENCES connector_providers(type_code, code)
);

CREATE INDEX idx_connectors_tenant_id ON connectors(tenant_id);
CREATE INDEX idx_connectors_type_code ON connectors(type_code);
CREATE INDEX idx_connectors_provider_code ON connectors(provider_code);
CREATE INDEX idx_connectors_direction_code ON connectors(direction_code);
CREATE INDEX idx_connectors_config_gin ON connectors USING GIN(config);
```

**Notes:**
- Uses `type_code`, `provider_code`, and `direction_code` to reference lookup tables
- Foreign key composite constraint validates provider belongs to correct type
- All enum values are now managed through lookup tables for i18n support
- `config` and `schema` use JSONB for flexible storage
- GIN index on `config` for fast JSON queries
- Unique constraint on (tenant_id, name) for tenant isolation
- New providers and directions can be added dynamically without schema changes

---

### Transformations Table

```sql
CREATE TABLE transformations (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id UUID NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    name VARCHAR(100) NOT NULL,
    type_code VARCHAR(50) NOT NULL REFERENCES transformation_types(code),
    description TEXT,
    connector_id UUID REFERENCES connectors(id) ON DELETE SET NULL,
    config JSONB NOT NULL,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    UNIQUE(tenant_id, name)
);

CREATE INDEX idx_transformations_tenant_id ON transformations(tenant_id);
CREATE INDEX idx_transformations_type_code ON transformations(type_code);
CREATE INDEX idx_transformations_connector_id ON transformations(connector_id);
```

**Note:** Uses `type_code` to reference `transformation_types` lookup table.

---

### Pipelines Table

```sql
CREATE TABLE pipelines (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id UUID NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    name VARCHAR(100) NOT NULL,
    description TEXT,
    source_id UUID NOT NULL REFERENCES connectors(id) ON DELETE RESTRICT,
    destination_id UUID NOT NULL REFERENCES connectors(id) ON DELETE RESTRICT,
    status_code VARCHAR(20) NOT NULL DEFAULT 'idle' REFERENCES pipeline_statuses(code),
    field_mappings JSONB NOT NULL,
    transformation_ids UUID[],
    is_scheduled BOOLEAN DEFAULT false,
    schedule JSONB,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    last_run_at TIMESTAMP WITH TIME ZONE,
    UNIQUE(tenant_id, name),
    CHECK (source_id != destination_id)
);

CREATE INDEX idx_pipelines_tenant_id ON pipelines(tenant_id);
CREATE INDEX idx_pipelines_source_id ON pipelines(source_id);
CREATE INDEX idx_pipelines_destination_id ON pipelines(destination_id);
CREATE INDEX idx_pipelines_status_code ON pipelines(status_code);
CREATE INDEX idx_pipelines_is_scheduled ON pipelines(is_scheduled);
CREATE INDEX idx_pipelines_transformation_ids_gin ON pipelines USING GIN(transformation_ids);
```

**Note:** Uses `status_code` to reference `pipeline_statuses` lookup table.

**Notes:**
- `transformation_ids` is a UUID array for transformation references
- `RESTRICT` on connector deletes prevents deletion if used by pipelines
- Check constraint ensures source != destination

---

### Pipeline Executions Table

```sql
CREATE TABLE pipeline_executions (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id UUID NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    pipeline_id UUID NOT NULL REFERENCES pipelines(id) ON DELETE CASCADE,
    status_code VARCHAR(20) NOT NULL REFERENCES execution_statuses(code),
    start_time TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    end_time TIMESTAMP WITH TIME ZONE,
    duration_ms INTEGER,
    records_processed INTEGER DEFAULT 0,
    errors TEXT[],
    progress_percent INTEGER DEFAULT 0 CHECK (progress_percent >= 0 AND progress_percent <= 100),
    logs JSONB
);

CREATE INDEX idx_executions_tenant_id ON pipeline_executions(tenant_id);
CREATE INDEX idx_executions_pipeline_id ON pipeline_executions(pipeline_id);
CREATE INDEX idx_executions_status_code ON pipeline_executions(status_code);
CREATE INDEX idx_executions_start_time ON pipeline_executions(start_time DESC);
```

**Note:** Uses `status_code` to reference `execution_statuses` lookup table.

**Notes:**
- `errors` is TEXT array for error messages
- `logs` is JSONB array for structured log entries
- Index on `start_time DESC` for recent executions queries

---

## Tenant Isolation Strategy

### Row-Level Security (RLS)

Enable RLS for all tenant-scoped tables:

```sql
-- Example for pipelines table
ALTER TABLE pipelines ENABLE ROW LEVEL SECURITY;

-- Policy to restrict access to tenant's own data
CREATE POLICY tenant_isolation_policy ON pipelines
    USING (tenant_id = current_setting('app.current_tenant_id', true)::uuid);

-- Policy for admins to see all data
CREATE POLICY admin_all_access ON pipelines
    USING (current_setting('app.user_role', true) = 'Admin');
```

### Setting Tenant Context

**In your API, set the tenant context per request:**

```csharp
// C# example with Npgsql
await using var conn = await dataSource.OpenConnectionAsync();
await using var cmd = new NpgsqlCommand(
    "SET app.current_tenant_id = @tenantId; SET app.user_role = @role;", 
    conn);
cmd.Parameters.AddWithValue("tenantId", tenantId);
cmd.Parameters.AddWithValue("role", userRole);
await cmd.ExecuteNonQueryAsync();

// Now all queries automatically filter by tenant_id
```

---

## JSONB Schema Examples

### Connector Config (Database)

```json
{
  "server": "db.example.com",
  "port": "5432",
  "database": "sales_db",
  "username": "app_user",
  "password": "encrypted_password",
  "writeConfig": {
    "tableName": "orders",
    "operation": "UPSERT",
    "primaryKeys": ["order_id"],
    "batchSize": 1000
  }
}
```

### Field Mappings

```json
[
  {
    "id": "550e8400-e29b-41d4-a716-446655440000",
    "sourceFields": ["OrderId"],
    "destinationField": "SaleId",
    "transformations": []
  },
  {
    "id": "6ba7b810-9dad-11d1-80b4-00c04fd430c8",
    "sourceFields": ["TotalAmount"],
    "destinationField": "Amount",
    "transformations": [
      {
        "transformationId": "7c9e6679-7425-40de-944b-e07fc1f90ae7"
      }
    ]
  }
]
```

### Execution Logs

```json
[
  {
    "timestamp": "2025-11-19T03:44:49-05:00",
    "level": "INFO",
    "message": "Pipeline execution started"
  },
  {
    "timestamp": "2025-11-19T03:45:00-05:00",
    "level": "INFO",
    "message": "Extracted 12345 records"
  }
]
```

---

## Indexes and Performance

### Recommended Indexes

1. **Foreign Keys**: Always index FK columns
2. **Tenant ID**: Critical for tenant isolation queries
3. **Status Fields**: For filtering (pipeline status, execution status)
4. **Timestamps**: For date range queries and sorting
5. **JSONB Fields**: Use GIN indexes for JSON queries

### Query Performance Tips

```sql
-- Good: Uses tenant_id index
SELECT * FROM pipelines WHERE tenant_id = $1 AND status = 'Running';

-- Bad: Full table scan
SELECT * FROM pipelines WHERE config->>'type' = 'Database';

-- Better: GIN index on config
SELECT * FROM pipelines WHERE config @> '{"type": "Database"}';
```

---

## Data Types Summary

| Field Type | PostgreSQL Type | JSON/API Type | Example |
|------------|----------------|---------------|---------|
| ID | `UUID` | `string` | `"550e8400-..."` |
| Name | `VARCHAR(100)` | `string` | `"Sales Pipeline"` |
| Description | `TEXT` | `string` | `"Long text..."` |
| Boolean | `BOOLEAN` | `boolean` | `true` |
| Timestamp | `TIMESTAMP WITH TIME ZONE` | `string (ISO 8601)` | `"2025-11-19T03:44:49-05:00"` |
| Config/Schema | `JSONB` | `object` | `{"key": "value"}` |
| Array | `UUID[]` or `TEXT[]` | `array` | `["id1", "id2"]` |
| Enum | `VARCHAR` with `CHECK` | `string` | `"Running"` |

---

## Migrations Strategy

### Use a Migration Tool

Recommended: **Entity Framework Core Migrations** or **Flyway** or **DbUp**

**Example migration structure:**
```
migrations/
├── V1__Create_tenants_table.sql
├── V2__Create_users_table.sql
├── V3__Create_connectors_table.sql
├── V4__Create_transformations_table.sql
├── V5__Create_pipelines_table.sql
├── V6__Create_executions_table.sql
└── V7__Enable_row_level_security.sql
```

---

## Backup and Maintenance

### Regular Backups

```bash
# Automated daily backups
pg_dump -h localhost -U postgres -F c -b -v -f "backup_$(date +%Y%m%d).dump" etl_db
```

### Vacuum and Analyze

```sql
-- Regular maintenance
VACUUM ANALYZE pipelines;
VACUUM ANALYZE pipeline_executions;

-- Auto-vacuum should be enabled (default in PostgreSQL 12+)
```

---

## Security Best Practices

1. **Encrypt passwords**: Use bcrypt with salt rounds >= 10
2. **Connection strings**: Never store in plaintext, use encrypted config
3. **API keys**: Hash before storing in database
4. **Row-Level Security**: Always enable for tenant isolation
5. **Principle of least privilege**: Use separate DB users for app vs admin
6. **SSL/TLS**: Always use encrypted connections in production

---

## Connection Pooling

**Recommended settings for ASP.NET Core with Npgsql:**

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=etl_db;Username=app_user;Password=***;Minimum Pool Size=10;Maximum Pool Size=100;Connection Idle Lifetime=300"
  }
}
```

---

## Dynamic Provider Management

### Adding New Providers via API

Providers can be added dynamically without code deployment:

**Admin API Endpoint:**
```
POST /api/admin/connector-providers
```

**Request:**
```json
{
  "typeCode": "database",
  "code": "mariadb",
  "displayName": "MariaDB",
  "isActive": true
}
```

**Backend Implementation:**
```csharp
[Authorize(Roles = "Admin")]
[HttpPost("admin/connector-providers")]
public async Task<IActionResult> CreateProvider([FromBody] CreateProviderDto request)
{
    // Validate type exists
    var typeExists = await _db.ConnectorTypes
        .AnyAsync(t => t.Code == request.TypeCode);
    
    if (!typeExists)
        return BadRequest("Invalid type code");
    
    // Check for duplicates
    var exists = await _db.ConnectorProviders
        .AnyAsync(p => p.TypeCode == request.TypeCode && 
                      (p.Code == request.Code || p.DisplayName == request.DisplayName));
    
    if (exists)
        return Conflict("Provider already exists");
    
    // Create new provider
    var provider = new ConnectorProvider
    {
        Id = Guid.NewGuid(),
        TypeCode = request.TypeCode,
        Code = request.Code.ToLowerInvariant(),
        DisplayName = request.DisplayName,
        IsActive = request.IsActive,
        CreatedAt = DateTime.UtcNow
    };
    
    _db.ConnectorProviders.Add(provider);
    await _db.SaveChangesAsync();
    
    return CreatedAtAction(nameof(GetProvider), new { id = provider.Id }, provider);
}
```

---

### API Storage vs Response Strategy

**Database Storage (Codes):**
```sql
-- Stored in database
INSERT INTO connectors (type_code, provider_code, ...)
VALUES ('database', 'sqlserver', ...);
```

**API Response (Display Names):**
```csharp
// When returning connector to client
public async Task<ConnectorDto> GetConnector(Guid id)
{
    var connector = await _db.Connectors
        .Include(c => c.Type)
        .Include(c => c.Provider)
        .FirstOrDefaultAsync(c => c.Id == id);
    
    return new ConnectorDto
    {
        Id = connector.Id,
        Type = connector.Type.DisplayName,      // "Database" (not "database")
        Provider = connector.Provider.DisplayName,  // "SQL Server" (not "sqlserver")
        Direction = connector.Direction,
        // ... other fields
    };
}
```

**When Creating Connector:**
```csharp
// Client sends display names
{
  "type": "Database",
  "provider": "SQL Server"
}

// Backend converts to codes and validates
var provider = await _db.ConnectorProviders
    .FirstOrDefaultAsync(p => 
        p.Type.DisplayName == request.Type && 
        p.DisplayName == request.Provider);

if (provider == null)
    return BadRequest("Invalid provider");

connector.TypeCode = provider.TypeCode;
connector.ProviderCode = provider.Code;
```

---

### Metadata Service Query

**SQL Query for Metadata API:**
```sql
-- Get all active providers grouped by type
SELECT 
    ct.code as type_code,
    ct.display_name as type_name,
    ct.icon as type_icon,
    json_agg(
        json_build_object(
            'code', cp.code,
            'displayName', cp.display_name,
            'labelKey', 'connectors.providers.' || cp.code
        ) ORDER BY cp.display_name
    ) as providers
FROM connector_types ct
LEFT JOIN connector_providers cp ON ct.code = cp.type_code
WHERE ct.is_active = true AND cp.is_active = true
GROUP BY ct.code, ct.display_name, ct.icon
ORDER BY ct.display_name;
```

**Response:**
```json
{
  "types": [
    {
      "code": "database",
      "displayName": "Database",
      "icon": "mdi-database",
      "labelKey": "connectors.types.database"
    }
  ],
  "providers": {
    "database": [
      {
        "code": "sqlserver",
        "displayName": "SQL Server",
        "labelKey": "connectors.providers.sqlserver"
      },
      {
        "code": "postgresql",
        "displayName": "PostgreSQL",
        "labelKey": "connectors.providers.postgresql"
      }
    ]
  }
}
```

---

## Future Enhancements

### Partitioning for Executions

For large datasets, consider partitioning `pipeline_executions` by date:

```sql
CREATE TABLE pipeline_executions (
    -- fields...
    start_time TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP
) PARTITION BY RANGE (start_time);

CREATE TABLE pipeline_executions_2025_11 PARTITION OF pipeline_executions
    FOR VALUES FROM ('2025-11-01') TO ('2025-12-01');
```

### Materialized Views for Dashboard

```sql
CREATE MATERIALIZED VIEW dashboard_stats AS
SELECT 
    tenant_id,
    COUNT(*) FILTER (WHERE status = 'Running') as active_pipelines,
    COUNT(DISTINCT id) as total_pipelines
FROM pipelines
GROUP BY tenant_id;

CREATE UNIQUE INDEX ON dashboard_stats (tenant_id);

-- Refresh periodically
REFRESH MATERIALIZED VIEW CONCURRENTLY dashboard_stats;
```
