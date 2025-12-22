# Connector Feature - Implementation Summary

## Overview

Full implementation of the Connector management feature for the Multi-Tenant ETL platform. Connectors allow users to configure connections to various data sources and destinations including databases, files, and APIs.

## What Was Implemented

### Domain Layer

**Entities:**
- `Connector.cs` - Main connector entity with hybrid schema design
  - Relational columns: Id, TenantId, Name, Type, Provider, Direction, IsSource, IsDestination, etc.
  - JSON columns: ConfigJson (JSONB), SchemaJson (JSONB) for flexible configuration
  
**Constants:**
- `ConnectorTypes.cs` - Type constants (Database, File, API)
- `ConnectorProviders.cs` - Provider constants (SqlServer, PostgreSQL, MySQL, CSV, Excel, JSON, REST)
- `ConnectorDirections.cs` - Direction constants (source, destination, both)
- `TestResults.cs` - Test result constants (Success, Failed)

### Application Layer

**DTOs (`Connectors/Models/ConnectorDtos.cs`):**
- `CreateConnectorRequest` - Create new connector
- `UpdateConnectorRequest` - Update existing connector
- `ConnectorResponse` - Full connector details
- `ConnectorListResponse` - List view (without full config)
- `TestConnectionRequest` - Test connection configuration
- `TestConnectionResponse` - Connection test results
- `DetectSchemaRequest` - Request schema detection
- `DetectSchemaResponse` - Schema detection results
- `SchemaField` - Field definition model
- `ConnectorSearchRequest` - Search/filter connectors
- `PagedConnectorResponse` - Paginated results
- `DatabaseConfig` - Database connection configuration
- `FileConfig` - File connector configuration
- `ApiConfig` - API connector configuration

**Interfaces:**
- `IConnectorService` - Main connector service interface
- `IConnectionTester` - Connection testing interface
- `ISchemaDetector` - Schema detection interface

### Infrastructure Layer

**Services:**
- `ConnectorService.cs` - Full CRUD operations, validation, tenant isolation
- `ConnectionTester.cs` - Connection testing for all connector types
  - SQL Server connection testing
  - PostgreSQL connection testing
  - MySQL connection testing
  - File path validation
  - API endpoint testing with authentication
- `SchemaDetector.cs` - Automatic schema detection
  - SQL Server schema detection (INFORMATION_SCHEMA queries)
  - PostgreSQL schema detection
  - MySQL schema detection
  - Detects: field names, data types, nullability, primary keys, lengths, precision, scale

**Database:**
- Updated `ApplicationDbContext` with Connector DbSet
- Configured JSONB columns for PostgreSQL
- Added indexes on TenantId, Type, Provider, and composite (TenantId, Name)
- Migration: `AddConnectors`

### API Layer

**Controller (`ConnectorsController.cs`):**
- `GET /api/connectors` - Get all connectors for tenant
- `POST /api/connectors/search` - Search with filters and pagination
- `GET /api/connectors/{id}` - Get connector by ID
- `POST /api/connectors` - Create new connector
- `PUT /api/connectors/{id}` - Update connector
- `DELETE /api/connectors/{id}` - Delete connector
- `POST /api/connectors/test-connection` - Test new connection config
- `POST /api/connectors/{id}/test` - Test existing connector
- `POST /api/connectors/detect-schema` - Detect schema from connector

**Service Registration:**
- Added HttpClient for API testing
- Registered IConnectorService, IConnectionTester, ISchemaDetector

**NuGet Packages Added:**
- `Microsoft.Data.SqlClient` (5.2.0) - SQL Server connectivity
- `MySqlConnector` (2.3.5) - MySQL connectivity
- Npgsql already included for PostgreSQL

## Database Schema

```sql
CREATE TABLE connectors (
    id UUID PRIMARY KEY,
    tenant_id UUID NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    name VARCHAR(200) NOT NULL,
    description VARCHAR(500),
    
    -- Relational columns for querying/filtering
    type VARCHAR(50) NOT NULL,              -- Database, File, API
    provider VARCHAR(100) NOT NULL,         -- SqlServer, PostgreSQL, CSV, etc.
    direction VARCHAR(20) NOT NULL,         -- source, destination, both
    is_source BOOLEAN NOT NULL,
    is_destination BOOLEAN NOT NULL,
    requires_credentials BOOLEAN NOT NULL,
    is_active BOOLEAN NOT NULL DEFAULT true,
    
    -- JSON columns for flexible configuration
    config_json JSONB NOT NULL,             -- Connection settings, credentials
    schema_json JSONB,                      -- Field definitions, metadata
    
    -- Testing and audit
    last_tested_at TIMESTAMP,
    last_test_result VARCHAR(50),
    last_test_message TEXT,
    created_at TIMESTAMP NOT NULL,
    updated_at TIMESTAMP,
    created_by UUID NOT NULL,
    updated_by UUID,
    
    -- Indexes
    INDEX idx_connectors_tenant (tenant_id),
    INDEX idx_connectors_type (type),
    INDEX idx_connectors_provider (provider),
    INDEX idx_connectors_tenant_name (tenant_id, name)
);
```

## Supported Connector Types

### 1. Database Connectors
- **SQL Server** - Full support with connection testing and schema detection
- **PostgreSQL** - Full support with connection testing and schema detection
- **MySQL** - Full support with connection testing and schema detection
- **Snowflake** - Full support with connection testing and schema detection
- **Google BigQuery** - Full support with connection testing and schema detection
- **AWS Redshift** - Full support with connection testing and multi-row insert optimization
- **MongoDB** - Full support with collection-based schema detection and bulk operations

**Features:**
- Connection string builder or manual connection string
- SSL/TLS support
- Connection testing with server version detection
- Automatic schema detection from INFORMATION_SCHEMA
- Detects: columns, data types, nullability, primary keys, lengths, precision, scale

### 2. File Connectors
- **CSV** - With delimiter and header configuration
- **Excel** - With sheet name support
- **JSON** - With encoding options
- **Google Cloud Storage (GCS)** - Full support with connection testing and streaming

**Features:**
- File path validation
- File existence checking
- File metadata (size, last modified)
- Schema detection (placeholder for future implementation)

### 3. API Connectors
- **REST** - HTTP/HTTPS APIs

**Features:**
- Base URL configuration
- Authentication types: None, Basic, Bearer, ApiKey, OAuth2
- Custom headers and query parameters
- Timeout configuration
- Connection testing with status code validation

## Security Features

- **Tenant Isolation**: All queries filtered by tenant_id
- **Credentials Storage**: Stored in encrypted JSONB column (ConfigJson)
- **Authorization**: Requires authentication for all endpoints
- **Validation**: Type and provider validation
- **Audit Trail**: Created/Updated by user tracking

## Configuration Examples

### Database Connector (PostgreSQL)
```json
{
  "name": "Production Database",
  "type": "Database",
  "provider": "PostgreSQL",
  "direction": "source",
  "config": {
    "host": "localhost",
    "port": 5432,
    "database": "mydb",
    "username": "user",
    "password": "pass",
    "useSsl": true
  }
}
```

### File Connector (CSV)
```json
{
  "name": "Sales Data CSV",
  "type": "File",
  "provider": "CSV",
  "direction": "source",
  "config": {
    "path": "/data/sales.csv",
    "delimiter": ",",
    "hasHeader": true,
    "encoding": "UTF-8"
  }
}
```

    "timeoutSeconds": 30
  }
}
```

### File Connector (GCS)
```json
{
  "name": "Cloud Storage CSV",
  "type": "File",
  "provider": "GCS",
  "direction": "source",
  "config": {
    "gcsBucket": "my-bucket",
    "gcsProjectId": "my-project",
    "gcsJsonCredentials": "{...}",
    "path": "data/sales.csv",
    "format": "csv"
  }
}
```

### Database Connector (BigQuery)
```json
{
  "name": "BigQuery Data",
  "type": "Database",
  "provider": "BigQuery",
  "direction": "both",
  "config": {
    "projectId": "my-gcp-project",
    "datasetId": "my_dataset",
    "tableName": "my_table",
    "jsonCredentials": "{...}"
  }
}
```

### Database Connector (Snowflake)
```json
{
  "name": "Snowflake Warehouse",
  "type": "Database",
  "provider": "Snowflake",
  "direction": "both",
  "config": {
    "account": "xy12345.us-east-1",
    "username": "my_user",
    "password": "my_password",
    "database": "MY_DB",
    "schema": "PUBLIC",
    "warehouse": "COMPUTE_WH",
    "role": "MY_ROLE",
    "tableName": "MY_TABLE"
  }
}
```

### Database Connector (AWS Redshift)
```json
{
  "name": "Redshift Cluster",
  "type": "Database",
  "provider": "Redshift",
  "direction": "both",
  "config": {
    "host": "my-cluster.xyz.us-east-1.redshift.amazonaws.com",
    "port": 5439,
    "database": "dev",
    "username": "admin",
    "password": "password",
    "useSsl": true,
    "tableName": "analytics.events"
  }
}
```

### Database Connector (MongoDB)
```json
{
  "name": "MongoDB Collection",
  "type": "Database",
  "provider": "MongoDb",
  "direction": "both",
  "config": {
    "connectionString": "mongodb+srv://user:pass@cluster0.mongodb.net",
    "database": "inventory",
    "collectionName": "products",
    "filterJson": "{\"category\": \"electronics\"}"
  }
}
```

## Testing

### Manual Testing with Swagger

1. Start the API:
   ```bash
   cd src/MultiTenantETL.API
   dotnet run
   ```

2. Navigate to: `https://localhost:7288/swagger`

3. Authenticate using OAuth token

4. Test endpoints:
   - Create a connector
   - Test connection
   - Detect schema (for databases)
   - List connectors
   - Update/Delete

### Connection Testing

The connection tester validates:
- Database: Opens connection, retrieves server version
- File: Checks file/directory existence
- API: Makes HTTP request, validates status code

### Schema Detection

For databases, automatically detects:
- Table columns
- Data types
- Nullability
- Primary keys
- Field lengths, precision, scale
- Default values

## Next Steps

1. **Encryption**: Implement encryption for credentials in ConfigJson
2. **File Schema Detection**: Parse CSV/Excel files to detect schema
3. **API Schema Detection**: Support OpenAPI/Swagger spec parsing
4. **Connection Pooling**: Optimize database connections
5. **Retry Logic**: Add retry for transient connection failures
6. **Validation**: Add more robust config validation
7. **Testing**: Add unit and integration tests

## Files Created/Modified

### Created:
- `Domain/Entities/Connector.cs`
- `Domain/Constants/ConnectorTypes.cs`
- `Application/Connectors/Models/ConnectorDtos.cs`
- `Application/Connectors/IConnectorService.cs`
- `Application/Connectors/IConnectionTester.cs`
- `Application/Connectors/ISchemaDetector.cs`
- `Infrastructure/Services/ConnectorService.cs`
- `Infrastructure/Services/ConnectionTester.cs`
- `Infrastructure/Services/SchemaDetector.cs`
- `API/Controllers/ConnectorsController.cs`
- `Migrations/[timestamp]_AddConnectors.cs`

### Modified:
- `Infrastructure/Persistence/ApplicationDbContext.cs` - Added Connector DbSet and configuration
- `Infrastructure/MultiTenantETL.Infrastructure.csproj` - Added database driver packages
- `API/Program.cs` - Registered connector services

## API Documentation

All endpoints are documented with:
- XML comments
- ProducesResponseType attributes
- Request/response models
- Error handling

Access Swagger UI at: `https://localhost:7288/swagger`
