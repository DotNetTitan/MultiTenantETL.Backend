# Connectors API Specification

**Resource:** Data Sources/Connectors  
**Base Path:** `/api/connectors`  
**Version:** 2.0  
        {
          "name": "string",
          "type": "string|integer|decimal|date|datetime|timestamp|json|textLong",
          "length": "string (optional, e.g., '18,2' for decimal)",
          "required": boolean,
          "nullable": boolean,
          "description": "string",
          "isPrimaryKey": boolean
        }
      ],
      "version": number,
      "isManual": boolean,
      "lastModified": "2025-11-19T03:44:49-05:00"
    },
    "requiresCredentials": boolean,
    "isSource": boolean,  // Deprecated - use direction
    "isDestination": boolean  // Deprecated - use direction
  }
]
```

---

### GET /api/connectors/{id}

Gets a single connector by ID.

**URL Parameters:**
- `id` (string, required): Connector ID

**Success Response (200):**
Returns a single connector object with the same structure as GET /api/connectors array items.

**Error Responses:**
- `404 Not Found`: Connector not found

---

### POST /api/connectors

Creates a new connector.

**Request Body:**
```json
{
  "name": "string (required, 3-100 chars)",
  "description": "string (optional, max 500 chars)",
  "type": "Database|File|API (required)",
  "provider": "string (required)",
  "direction": "source|destination|both (required)",
  "config": {
    // Type-specific configuration (see below)
  },
  "schema": {
    // Optional - can be auto-detected or manually defined
    "fields": [...],
    "isManual": boolean
  },
  "requiresCredentials": boolean
}
```

**Success Response (201):**
Returns the created connector object with `id` and `createdAt` populated.

**Error Responses:**
- `400 Bad Request`: Validation error
- `409 Conflict`: Connector name already exists

---

### PUT /api/connectors/{id}

Updates an existing connector.

**URL Parameters:**
- `id` (string, required): Connector ID

**Request Body:**
Same as POST /api/connectors (all fields optional for partial update).

**Success Response (200):**
Returns the updated connector object.

**Error Responses:**
- `404 Not Found`: Connector not found
- `400 Bad Request`: Validation error

---

### DELETE /api/connectors/{id}

Deletes a connector.

**URL Parameters:**
- `id` (string, required): Connector ID

**Success Response (200):**
```json
{
  "success": true
}
```

**Error Responses:**
- `404 Not Found`: Connector not found
- `400 Bad Request`: Connector is in use by pipelines (provide list of pipelines)

---

### POST /api/connectors/test-connection

Tests connection to a connector without saving it.

**Request Body:**
Connector configuration object (same as POST /api/connectors).

**Success Response (200):**
```json
{
  "success": true,
  "message": "Connection successful",
  "connectionTime": 245,  // milliseconds
  "schema": {
    // Optional: Auto-detected schema if available
    "fields": [...]
  }
}
```

**Error Response (400):**
```json
{
  "success": false,
  "message": "Connection failed: Unable to connect to server",
  "error": "ETIMEDOUT",
  "details": "Connection timeout after 30000ms"
}
```

---

### GET /api/connectors/{id}/schema

Auto-detects and returns the schema for a connector.

**URL Parameters:**
- `id` (string, required): Connector ID

**Query Parameters:**
- `refresh` (boolean, optional): Force refresh schema detection (default: false)

**Success Response (200):**

**For Databases:**
```json
{
  "tables": [
    {
      "name": "Orders",
      "columns": [
        {
          "name": "OrderId",
          "type": "int",
          "nullable": false,
          "isPrimaryKey": true,
          "length": null
        },
        {
          "name": "TotalAmount",
          "type": "decimal",
          "nullable": false,
          "length": "18,2"
        }
      ]
    }
  ],
  "views": ["vw_OrderSummary"],
  "procedures": ["sp_ProcessOrder"]
}
```

**For Files (CSV, Excel, JSON):**
```json
{
  "fields": [
    {
      "name": "customer_id",
      "type": "string",
      "nullable": false,
      "sampleValues": ["C001", "C002", "C003"]
    },
    {
      "name": "email",
      "type": "string",
      "nullable": true
    }
  ],
  "rowCount": 1500,
  "detectedDelimiter": ",",
  "hasHeader": true
}
```

**For APIs:**
```json
{
  "endpoints": [
    {
      "path": "/products",
      "method": "GET",
      "responseSchema": {
        "fields": [
          {"name": "id", "type": "integer"},
          {"name": "name", "type": "string"}
        ]
      }
    }
  ]
}
```

**Error Responses:**
- `404 Not Found`: Connector not found
- `500 Internal Server Error`: Schema detection failed

---

### GET /api/connectors/sources

Gets all connectors that can be used as sources (direction = "source" or "both").

**Query Parameters:**
Same as GET /api/connectors

**Success Response (200):**
Returns array of connector objects filtered to sources only.

---

### GET /api/connectors/destinations

Gets all connectors that can be used as destinations (direction = "destination" or "both").

**Query Parameters:**
Same as GET /api/connectors

**Success Response (200):**
Returns array of connector objects filtered to destinations only.

---

## Configuration Structures

### Database Configuration

```json
{
  "server": "sales-db.example.com",
  "port": "1433",
  "database": "SalesDB",
  "username": "db_user",
  "password": "***",
  "useCustomConnectionString": false,
  "connectionString": "optional custom connection string",
  
  // Required if direction = "destination" or "both"
  "writeConfig": {
    "tableName": "Orders",
    "operation": "INSERT|UPDATE|UPSERT|BULK_INSERT",
    "primaryKeys": ["OrderId"],  // Required for UPDATE/UPSERT
    "batchSize": 1000  // Number of records per batch
  }
}
```

**Operation Types:**
- `INSERT` - Insert new records only
- `UPDATE` - Update existing records (requires primaryKeys)
- `UPSERT` - Insert or update (requires primaryKeys)
- `BULK_INSERT` - Fast bulk insert with no validation

---

### File Configuration

**CSV/Excel:**
```json
{
  "format": "CSV|Excel",
  "path": "/data/customers",
  "delimiter": ",",  // CSV only
  "hasHeader": true,
  
  // For cloud storage
  "storageType": "Local|FTP|SFTP|S3|Azure Blob|Google Cloud Storage",
  
  // FTP/SFTP specific
  "ftpHost": "ftp.example.com",
  "ftpPort": "21",
  "ftpUsername": "user",
  "ftpPassword": "***",
  
  // S3 specific
  "bucket": "my-bucket",
  "region": "us-east-1",
  "awsAccessKey": "***",
  "awsSecretKey": "***",
  
  // Azure Blob specific
  "azureAccountName": "myaccount",
  "azureContainer": "container",
  "azureAccountKey": "***",
  
  // GCS specific
  "gcsBucket": "my-bucket",
  "gcsProjectId": "project-123",
  "gcsCredentials": "***",
  
  // Required if direction = "destination" or "both"
  "writeConfig": {
    "writeMode": "overwrite|append",
    "filenamePattern": "export_{date}.csv",  // Supports {date}, {time}, {timestamp}
    "includeHeaders": true,  // CSV only
    "columnOrder": ["id", "name", "value"],  // CSV only
    "sheetName": "Data",  // Excel only
    "startCell": "A1"  // Excel only
  }
}
```

**JSON:**
```json
{
  "format": "JSON",
  "path": "/data/exports",
  "storageType": "Local|S3|Azure Blob|Google Cloud Storage",
  // ... cloud storage config same as above
  
  "writeConfig": {
    "writeMode": "overwrite|append",
    "filenamePattern": "export_{timestamp}.json",
    "structure": "array|object|nested",
    "rootKey": "data"  // Optional wrapper key
  }
}
```

**XML:**
```json
{
  "format": "XML",
  "path": "/data/imports/xml",
  "xmlRootElement": "Records",
  "xmlRecordElement": "Record"
}
```

**Parquet:**
```json
{
  "format": "Parquet",
  "path": "/raw-data/events",
  "storageType": "S3|Azure Blob|Google Cloud Storage",
  // ... cloud storage config
  
  "writeConfig": {
    "writeMode": "overwrite|append",
    "filenamePattern": "events_{date}_{time}.parquet",
    "compression": "snappy|gzip|none"
  }
}
```

---

### API Configuration

**REST API:**
```json
{
  "url": "https://api.example.com/v1",
  "authType": "None|Basic|Bearer|OAuth2|API Key",
  
  // Basic auth
  "username": "user",
  "password": "***",
  
  // Bearer token
  "token": "***",
  
  // OAuth2
  "clientId": "***",
  "clientSecret": "***",
  "tokenUrl": "https://auth.example.com/token",
  
  // API Key
  "apiKey": "***",
  "apiKeyHeader": "X-API-Key",  // Header name for API key
  
  // Custom headers
  "headers": "{\"Custom-Header\": \"value\"}",
  
  // Endpoints configuration
  "endpoints": [
    {
      "id": "1",
      "method": "GET|POST|PUT|PATCH|DELETE",
      "path": "/products",
      "responseDataPath": "data",  // JSONPath to data array
      "requestDataPath": "product"  // JSONPath for request payload (write operations)
    }
  ],
  
  // Required if direction = "destination" or "both"
  "writeConfig": {
    "requestFormat": "JSON|XML|Form Data",
    "wrapInArray": false,  // Wrap single records in array
    "rootKey": "product",  // Wrapper key for payload
    "batchSize": 100  // Records per request
  }
}
```

**GraphQL:**
```json
{
  "url": "https://api.example.com/graphql",
  "authType": "Bearer|OAuth2",
  "token": "***",
  "query": "{ products { id name price } }"
}
```

**SOAP:**
```json
{
  "url": "https://api.example.com/soap/v1",
  "authType": "Basic|None",
  "username": "user",
  "password": "***",
  "wsdlUrl": "https://api.example.com/soap/v1?wsdl"
}
```

---

## Field Data Types

Supported data types in connector schemas:

- `string` - Text/varchar
- `integer` - 32-bit integer
- `bigInteger` - 64-bit integer
- `decimal` - Decimal/numeric with precision
- `boolean` - True/false
- `date` - Date only (no time)
- `dateTime` - Date and time
- `timestamp` - Unix timestamp or datetime with timezone
- `json` - JSON object
- `textLong` - Large text fields (TEXT, CLOB, etc.)

---

## Examples

### Example 1: SQL Server Database Connector

```json
{
  "name": "SQL Server - Sales",
  "description": "Main sales database",
  "type": "Database",
  "provider": "SQL Server",
  "direction": "both",
  "config": {
    "server": "sales-db.example.com",
    "port": "1433",
    "database": "SalesDB",
    "username": "sa",
    "password": "***",
    "writeConfig": {
      "tableName": "Orders",
      "operation": "UPSERT",
      "primaryKeys": ["OrderId"],
      "batchSize": 1000
    }
  },
  "schema": {
    "fields": [
      {
        "name": "OrderId",
        "type": "integer",
        "required": true,
        "nullable": false,
        "isPrimaryKey": true
      },
      {
        "name": "TotalAmount",
        "type": "decimal",
        "length": "18,2",
        "required": true,
        "nullable": false
      }
    ],
    "version": 1,
    "isManual": false
  },
  "requiresCredentials": true
}
```

### Example 2: AWS S3 JSON File Connector

```json
{
  "name": "AWS S3 Bucket",
  "description": "S3 bucket for data lake storage",
  "type": "File",
  "provider": "S3",
  "direction": "both",
  "config": {
    "format": "JSON",
    "bucket": "my-data-lake",
    "region": "us-east-1",
    "path": "/raw-data",
    "awsAccessKey": "***",
    "awsSecretKey": "***",
    "writeConfig": {
      "writeMode": "append",
      "filenamePattern": "export_{timestamp}.json",
      "structure": "array"
    }
  },
  "requiresCredentials": true
}
```

### Example 3: REST API Connector

```json
{
  "name": "ERP API",
  "description": "REST API for ERP system",
  "type": "API",
  "provider": "REST",
  "direction": "both",
  "config": {
    "url": "https://erp.example.com/api/v1",
    "authType": "Bearer",
    "token": "***",
    "endpoints": [
      {
        "id": "1",
        "method": "GET",
        "path": "/products",
        "responseDataPath": "data"
      },
      {
        "id": "2",
        "method": "POST",
        "path": "/products",
        "requestDataPath": "product",
        "responseDataPath": "data"
      }
    ],
    "writeConfig": {
      "requestFormat": "JSON",
      "wrapInArray": false,
      "rootKey": "product",
      "batchSize": 100
    }
  },
  "requiresCredentials": true
}
```

---

## Validation Rules

### Name
- Required
- 3-100 characters
- Must be unique per tenant

### Identifier (if used)
- 3-50 characters
- Alphanumeric + underscore only
- Lowercase
- Must be unique per tenant

### Type
- Required
- Must be one of: Database, File, API

### Provider
- Required
- Must match available providers for the selected type

### Direction
- Required
- Must be one of: source, destination, both

### Config
- Required
- Must match structure for selected type
- Credentials required for cloud storage and databases

### WriteConfig
- Required if direction = "destination" or "both"
- Structure depends on connector type

---

## Error Codes

- `CONN_001`: Connection test failed
- `CONN_002`: Invalid configuration
- `CONN_003`: Schema detection failed
- `CONN_004`: Connector in use by pipelines
- `CONN_005`: Invalid credentials
- `CONN_006`: Unsupported provider
- `CONN_007`: Write configuration missing for destination
