# Connector Frontend Integration - Complete

## Overview

The frontend has been fully integrated with the backend Connector API. All connector operations now use real API calls instead of mock data.

## Snowflake Support

### Database Provider Support
- **Added Snowflake** to the list of supported database providers
- Updated mock metadata to include `Snowflake` in Database providers array

### Snowflake-Specific Configuration
Snowflake connectors use different connection parameters than traditional databases:

**Snowflake Config:**
```javascript
{
  account: 'your-account.snowflakecomputing.com',
  warehouse: 'COMPUTE_WH',
  database: 'ANALYTICS_DB',
  schema: 'PUBLIC',
  username: 'your_username',
  password: 'your_password',
  role: 'ACCOUNTADMIN'  // Optional
}
```

**Key Differences from Traditional Databases:**
- No `host`/`port` - uses `account` URL instead
- No `useSsl` - SSL is always enabled for Snowflake
- Additional `warehouse`, `schema`, and optional `role` fields

### Form Validation
- **Snowflake-specific validation** checks for account, warehouse, database, schema, username, and password
- **Provider-aware validation** switches validation rules based on selected provider
- **Dynamic form fields** show/hide appropriate fields based on provider selection

### UI Components
- **Conditional rendering** in ConnectorWizard shows Snowflake fields when Snowflake is selected
- **Provider-specific placeholders** guide users with appropriate examples
- **Multi-language support** includes Snowflake field labels in all supported languages

### Error Handling
Added specific error messages for common Snowflake connection issues:
- Invalid account URL
- Warehouse not found
- Database/schema not found
- Insufficient privileges
- Session expired

## Changes Made

### 1. Service Layer (`src/services/connectorService.js`)

**Updated Methods:**
- `fetchConnectors()` - Now uses POST `/connectors/search` with pagination
- `fetchConnectorById()` - Uses GET `/connectors/{id}`
- `createConnector()` - Uses POST `/connectors`
- `updateConnector()` - Uses PUT `/connectors/{id}`
- `deleteConnector()` - Uses DELETE `/connectors/{id}`
- `testConnection()` - Uses POST `/connectors/test-connection`
- `testExistingConnection()` - Uses POST `/connectors/{id}/test` (NEW)
- `detectSchema()` - Uses POST `/connectors/detect-schema`
- `getAllConnectors()` - Uses GET `/connectors` (NEW)

**Removed:**
- All mock implementations
- Mock data imports

### 2. Composable (`src/composables/useConnector.js`)

**Updated:**
- `validateConnection()` - Now calls real API via `connectorService.testConnection()`
- `testExistingConnection()` - New method for testing saved connectors
- `detectSchema()` - Now calls real API with connectorId and optional table name

**Removed:**
- All mock implementations
- Connector format mapping (using backend format directly)

### 3. Connector Wizard (`src/components/connector/ConnectorWizard.vue`)

**Updated to Backend Format:**

**Database Config:**
```javascript
{
  host: '',           // was: server
  port: null,
  database: '',       // was: databaseName
  username: '',
  password: '',
  useSsl: false,      // NEW
  useCustomConnectionString: false,
  connectionString: null
}
```

**API Config:**
```javascript
{
  baseUrl: '',        // was: url
  authType: 'None',
  authToken: null,    // was: token
  username: null,
  password: null,
  headers: {},        // was: string, now object
  queryParameters: {},// NEW
  timeoutSeconds: 30  // NEW
}
```

**File Config:**
```javascript
{
  path: '',
  delimiter: ',',
  hasHeader: true,
  sheetName: null,
  encoding: 'UTF-8'   // NEW
}
```

**Simplified Validation:**
- Removed complex nested validation
- Now validates directly against backend format
- Removed write config validation (will be added when implementing pipelines)

### 4. Data Format

**Consistent Backend Format:**
All connector data now uses the backend API format:

```json
{
  "id": "guid",
  "tenantId": "guid",
  "name": "My Connector",
  "description": "Optional description",
  "type": "Database",
  "provider": "PostgreSQL",
  "direction": "source",
  "isSource": true,
  "isDestination": false,
  "requiresCredentials": true,
  "isActive": true,
  "config": {
    "host": "localhost",
    "port": 5432,
    "database": "mydb",
    "username": "user",
    "password": "pass",
    "useSsl": false
  },
  "schema": {
    "tableName": "users",
    "fields": [
      {
        "name": "id",
        "dataType": "int",
        "isNullable": false,
        "isPrimaryKey": true
      }
    ]
  },
  "lastTestedAt": "2024-01-01T00:00:00Z",
  "lastTestResult": "Success",
  "lastTestMessage": "Connection successful",
  "createdAt": "2024-01-01T00:00:00Z",
  "updatedAt": null
}
```

## API Endpoints Used

| Method | Endpoint | Purpose |
|--------|----------|---------|
| GET | `/api/connectors` | Get all active connectors |
| POST | `/api/connectors/search` | Search with filters/pagination |
| GET | `/api/connectors/{id}` | Get connector by ID |
| POST | `/api/connectors` | Create new connector |
| PUT | `/api/connectors/{id}` | Update connector |
| DELETE | `/api/connectors/{id}` | Delete connector |
| POST | `/api/connectors/test-connection` | Test new connection config |
| POST | `/api/connectors/{id}/test` | Test existing connector |
| POST | `/api/connectors/detect-schema` | Detect schema from connector |

## Testing

### Manual Testing Steps

1. **Start Backend:**
   ```bash
   cd MultiTenantETL/src/MultiTenantETL.API
   dotnet run
   ```

2. **Start Frontend:**
   ```bash
   cd MultiTenantETL.Vue
   npm run dev
   ```

3. **Test Connector Operations:**
   - Navigate to http://localhost:5173/connectors
   - Click "Create Connector"
   - Fill in connector details:
     - Name: "Test PostgreSQL"
     - Type: Database
     - Provider: PostgreSQL
     - Direction: source
     - Host: localhost
     - Port: 5432
     - Database: your_db
     - Username: your_user
     - Password: your_pass
   - Click "Test Connection" (should call backend)
   - Complete wizard and save
   - Verify connector appears in list
   - Test edit, delete operations

### Expected Behavior

- ✅ All operations should call real backend API
- ✅ Loading states should show during API calls
- ✅ Error messages should display from backend
- ✅ Connection testing should validate credentials
- ✅ Schema detection should work for databases
- ✅ Pagination should work in connector list
- ✅ Search/filter should work

## Error Handling

The service layer handles errors with user-friendly messages:

```javascript
try {
  const response = await api.post('/connectors', data)
  return response.data
} catch (error) {
  // error.userMessage is set by API interceptor
  throw error
}
```

Error messages are displayed in:
- Snackbar notifications (global)
- Inline validation messages (forms)
- Alert dialogs (critical errors)

## Authentication

All API calls include:
- **Authorization header:** `Bearer {access_token}`
- **Tenant header:** `X-Tenant-Id: {tenantId}`

These are automatically added by the Axios interceptor in `src/services/api.js`.

## Next Steps

1. **Schema Editor Integration:** Update SchemaEditor component to work with backend schema format
2. **File Upload:** Implement file upload for CSV/Excel schema detection
3. **API Endpoint Editor:** Simplify or remove (backend doesn't use complex endpoint config)
4. **Write Configuration:** Remove or simplify (will be part of pipeline configuration)
5. **Testing:** Add unit tests for service methods
6. **Error Handling:** Add more specific error messages for different scenarios

## Files Modified

### Updated:
- `src/services/connectorService.js` - Real API integration
- `src/composables/useConnector.js` - Real API calls
- `src/components/connector/ConnectorWizard.vue` - Backend format

### Removed:
- `src/utils/connectorMapper.js` - No longer needed

### Unchanged (should work as-is):
- `src/views/ConnectorsView.vue` - Uses service layer
- `src/components/connector/ConnectorForm.vue` - Uses service layer
- `src/components/connector/SchemaEditor.vue` - Schema format compatible
- `src/components/connector/SchemaPreview.vue` - Display only

## Configuration

No configuration changes needed. The API base URL is already configured in:
- Development: `http://localhost:5000/api` (default)
- Production: Set via `VITE_API_URL` environment variable

## Notes

- The frontend now uses the simpler backend format throughout
- No data transformation is needed between frontend and backend
- All connector types (Database, File, API) use consistent structure
- Schema is stored as JSON in the backend, parsed as needed in frontend
- Connection testing validates credentials before saving
- Schema detection is optional and can be done after connector creation
