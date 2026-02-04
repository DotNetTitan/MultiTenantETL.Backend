# Multi-Tenant ETL Platform - API Specification Index

**Version:** 2.0  
**Last Updated:** 2025-11-19  
**Base URL:** `http://localhost:5000/api`

---

## Overview

This API specification has been modularized for better maintainability. Each resource has its own detailed specification document.

---

## API Modules

### Core Resources

1. **[Connectors API](./connectors-api.md)**
   - Data source and destination management
   - Database, File, and API connectors
   - Connection testing and schema detection
   - Write configurations for destinations

2. **[Pipelines API](./pipelines-api.md)**
   - ETL pipeline configuration and management
   - Field mappings with transformation chains
   - Schedule configuration (daily, weekly, monthly)
   - Pipeline validation and execution

3. **[Transformations API](./transformations-api.md)**
   - Data transformation logic
   - 7 transformation types (Filter, Map, Trim, Case, Substring, Replace, Script)
   - JavaScript and C# custom scripts
   - Transformation validation

4. **[Executions API](./executions-api.md)**
   - Pipeline execution tracking
   - Real-time progress monitoring
   - Execution logs and error details
   - WebSocket support for live updates

### Supporting Resources

5. **[Dashboard API](./dashboard-api.md)**
   - Dashboard statistics
   - Status distribution
   - Recent execution activity

6. **[Metadata API](./metadata-api.md)**
   - Centralized configuration
   - Dropdown options and validation rules
   - Translation keys for i18n support

### User Management (See original specification)

7. **Authentication**
   - Login/logout
   - JWT token management

8. **Tenants**
   - Multi-tenant management
   - Tenant isolation

9. **Users**
   - User CRUD operations
   - Role-based access control

---

## Global Conventions

### Authentication

All endpoints except `/auth/login` require authentication.

**Headers:**
```
Authorization: Bearer {token}
X-Tenant-Id: {tenantId}
```

### Date Format

All dates use ISO 8601 format with timezone:
```
2025-11-19T03:44:49-05:00
```

### Error Responses

Standard error response format:
```json
{
  "error": "Error message",
  "code": "ERROR_CODE",
  "details": "Additional error details",
  "field": "fieldName (for validation errors)"
}
```

### HTTP Status Codes

- `200 OK` - Successful GET/PUT/DELETE
- `201 Created` - Successful POST (resource created)
- `400 Bad Request` - Validation error or invalid request
- `401 Unauthorized` - Authentication required or token invalid
- `403 Forbidden` - Insufficient permissions
- `404 Not Found` - Resource not found
- `409 Conflict` - Resource conflict (duplicate name, etc.)
- `422 Unprocessable Entity` - Semantic validation error
- `429 Too Many Requests` - Rate limit exceeded
- `500 Internal Server Error` - Server error
- `503 Service Unavailable` - Service temporarily unavailable

---

## Data Types & Implementation Strategy

### API Format vs Database Storage

The API uses **human-readable display names** for better developer experience, while the database stores **lowercase codes** for consistency and internationalization.

**Pattern:**

| Concept | API Value | Database Value | Lookup Table |
|---------|-----------|----------------|--------------|
| Connector Type | `"Database"` | `"database"` | `connector_types` |
| Provider | `"SQL Server"` | `"sqlserver"` | `connector_providers` |
| Direction | `"source"` | `"source"` | `connector_directions` |
| Transformation Type | `"Case Convert"` | `"case_convert"` | `transformation_types` |
| Pipeline Status | `"Running"` | `"running"` | `pipeline_statuses` |
| Execution Status | `"Completed"` | `"completed"` | `execution_statuses` |
| User Role | `"Admin"` | `"admin"` | `user_roles` |
| Schedule Frequency | `"Daily"` | `"daily"` | `schedule_frequencies` |

### Backend Translation Flow

**When Creating/Updating:**
```csharp
// 1. Client sends display name
POST /api/connectors { "type": "Database", "provider": "SQL Server" }

// 2. Backend validates and converts to codes
var provider = await _db.ConnectorProviders
    .FirstOrDefaultAsync(p => 
        p.Type.DisplayName == "Database" && 
        p.DisplayName == "SQL Server");

// 3. Stores codes in database
connector.TypeCode = "database";
connector.ProviderCode = "sqlserver";
```

**When Retrieving:**
```csharp
// 1. Query with joins to lookup tables
var connector = await _db.Connectors
    .Include(c => c.Type)
    .Include(c => c.Provider)
    .FirstOrDefaultAsync(c => c.Id == id);

// 2. Return display names in API response
return new ConnectorDto {
    Type = connector.Type.DisplayName,      // "Database"
    Provider = connector.Provider.DisplayName  // "SQL Server"
};
```

### Benefits of This Approach

✅ **User-Friendly API** - Developers work with readable strings, not cryptic codes  
✅ **Database Integrity** - Foreign keys prevent invalid values  
✅ **Internationalization** - Display names can be translated via i18n keys  
✅ **Dynamic Management** - Add new providers/types via admin API without code deployment  
✅ **Performance** - Lookup tables are small, heavily cached  
✅ **Consistency** - Single source of truth for valid values  

### Translation Keys (i18n)

Lookup tables include `labelKey` for frontend internationalization:

```json
{
  "code": "sqlserver",
  "displayName": "SQL Server",
  "labelKey": "connectors.providers.sqlserver"
}
```

Frontend uses the labelKey:
```javascript
// en.json
{ "connectors.providers.sqlserver": "SQL Server" }

// es.json
{ "connectors.providers.sqlserver": "SQL Server" }

// fr.json
{ "connectors.providers.sqlserver": "SQL Server" }
```

See [Database Implementation Guide](./database-implementation.md#lookup-tables-enums--reference-data) for complete schema details.

---

## Pagination (Future Enhancement)

**Recommended structure for all list endpoints:**

**Query Parameters:**
- `page` (number, default: 1)
- `pageSize` (number, default: 25, max: 100)

**Response:**
```json
{
  "items": [...],
  "pagination": {
    "page": 1,
    "pageSize": 25,
    "totalItems": 150,
    "totalPages": 6,
    "hasMore": true
  }
}
```

---

## Rate Limiting (Future Enhancement)

**Recommended limits:**
- Authentication endpoints: 5 requests/minute per IP
- Read endpoints: 100 requests/minute per user
- Write endpoints: 30 requests/minute per user

**Headers:**
```
X-RateLimit-Limit: 100
X-RateLimit-Remaining: 95
X-RateLimit-Reset: 1700000000
Retry-After: 60 (on 429 response)
```

---

## API Versioning

**Current Version:** v1 (implicit in `/api` base path)

**Future Versioning Strategy:**
- URL versioning: `/api/v2/`
- OR Header versioning: `API-Version: 2.0`

---

## Testing the API

### Development Server

```bash
# Backend should run on:
http://localhost:5000

# Frontend development server:
http://localhost:5173
```

### Mock Data

The frontend currently uses mock data in `src/mocks/` for development. These API specifications match the structure of the mock data to ensure seamless integration when the backend is ready.

---

## Implementation Priority

**Phase 1 (MVP):**
1. Authentication
2. Connectors (basic CRUD)
3. Pipelines (basic CRUD + field mappings)
4. Executions (basic tracking)
5. Dashboard (basic stats)

**Phase 2:**
6. Transformations
7. Real-time execution updates (WebSocket)
8. Advanced connector features (schema detection)

**Phase 3:**
9. Metadata service
10. Pagination
11. Advanced filtering and search
12. Rate limiting

---

## Technology Stack (Backend Recommendation)

- **Framework:** ASP.NET Core Web API
- **Database:** PostgreSQL
- **ORM:** Entity Framework Core
- **Authentication:** JWT Bearer tokens
- **Real-time:** SignalR (for execution progress)
- **Documentation:** Swagger/OpenAPI
- **Testing:** xUnit, Moq

---

## Next Steps

1. ✅ Review API specifications
2. ⏳ Set up ASP.NET Core project
3. ⏳ Configure PostgreSQL database
4. ⏳ Implement authentication endpoints
5. ⏳ Implement core CRUD operations
6. ⏳ Add validation and error handling
7. ⏳ Implement WebSocket for real-time updates
8. ⏳ Add comprehensive logging
9. ⏳ Write unit and integration tests
10. ⏳ Deploy to staging environment

---

## Contact

For questions or clarifications about the API specification:
- Review the detailed spec for each resource
- Check the mock data in `src/mocks/`
- Refer to service implementations in `src/services/`
