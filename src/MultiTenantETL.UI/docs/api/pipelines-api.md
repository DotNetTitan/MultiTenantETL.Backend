# Pipelines API Specification

**Resource:** Pipelines  
**Base Path:** `/api/pipelines`  
**Version:** 2.0  
**Last Updated:** 2025-11-19

---

## Overview

Pipelines define ETL (Extract, Transform, Load) workflows that move data from a source connector to a destination connector, optionally applying transformations.

### Key Features
- Field-level mapping between source and destination
- Transformation chains per field
- Scheduled or manual execution
- Execution history and monitoring
- Validation before execution

> **Backend Implementation Note:**  
> Pipeline statuses in the API (`"Idle"`, `"Running"`, `"Failed"`) are stored as codes (`"idle"`, `"running"`, `"failed"`) in the database. Schedule frequencies and other enum-like fields also use lookup tables. See [Database Implementation](./database-implementation.md#lookup-tables-enums--reference-data).

---

## Endpoints

### GET /api/pipelines

Gets all pipelines with optional filtering and sorting.

**Query Parameters:**
- `search` (string, optional): Search in name, description, sourceName, destinationName
- `status` (string, optional): Filter by status - "Idle", "Running", "Failed", "All" (default: "All")
- `sort` (string, optional): Sort field and direction
  - `name_asc` | `name_desc`
  - `createdAt_asc` | `createdAt_desc`
  - `lastRun_asc` | `lastRun_desc` (sorts by lastRunAt)
  - `status_asc` | `status_desc`

**Success Response (200):**
```json
[
  {
    "id": "string",
    "name": "string",
    "description": "string",
    "sourceName": "string (connector name)",
    "destinationName": "string (connector name)",
    "status": "Idle|Running|Failed",
    "sourceId": "string (connector ID)",
    "destinationId": "string (connector ID)",
    "transformationIds": ["string"],  // Array of transformation IDs
    "transformations": [],  // Array of transformation objects (populated on detail view)
    "fieldMappings": [
      {
        "id": "string",
        "sourceFields": ["string"],  // Array to support concatenation
        "destinationField": "string",
        "transformations": [
          {
            "transformationId": "string"
          }
        ]
      }
    ],
    "isScheduled": boolean,
    "schedule": {
      "frequency": "Daily|Weekly|Monthly",
      "time": "02:00",  // HH:mm format
      "dayOfWeek": 1,  // 1=Monday, 7=Sunday (for weekly)
      "dayOfMonth": 1,  // 1-31 (for monthly)
      "cronExpression": "0 2 * * *",  // Cron format
      "timezone": "UTC|America/New_York|America/Los_Angeles|..."
    },
    "createdAt": "2025-11-19T03:44:49-05:00",
    "lastRunAt": "2025-11-19T03:44:49-05:00"
  }
]
```

---

### GET /api/pipelines/{id}

Gets a single pipeline by ID with full details including transformations.

**URL Parameters:**
- `id` (string, required): Pipeline ID

**Success Response (200):**
Returns a single pipeline object with the same structure as GET /api/pipelines, but with `transformations` array populated with full transformation objects.

**Error Responses:**
- `404 Not Found`: Pipeline not found

---

### POST /api/pipelines

Creates a new pipeline.

**Request Body:**
```json
{
  "name": "string (required, 3-100 chars)",
  "description": "string (optional, max 500 chars)",
  "sourceId": "string (required, connector ID)",
  "destinationId": "string (required, connector ID)",
  "transformationIds": ["string"],  // Optional array of transformation IDs
  "fieldMappings": [
    {
      "sourceFields": ["OrderId"],  // Required, array of source field names
      "destinationField": "SaleId",  // Required
      "transformations": [
        {
          "transformationId": "1"  // Optional, ID of transformation to apply
        }
      ]
    }
  ],
  "isScheduled": boolean,
  "schedule": {
    // Required if isScheduled = true
    "frequency": "Daily|Weekly|Monthly",
    "time": "02:00",
    "dayOfWeek": 1,  // Required if frequency = "Weekly"
    "dayOfMonth": 1,  // Required if frequency = "Monthly"
    "cronExpression": "0 2 * * *",  // Optional custom cron
    "timezone": "UTC"
  }
}
```

**Success Response (201):**
Returns the created pipeline object with `id`, `status` (Idle), `createdAt`, and `lastRunAt` (null) populated.

**Error Responses:**
- `400 Bad Request`: Validation error
  - Source connector not found
  - Destination connector not found
  - Field mapping validation failed
  - Invalid schedule configuration
- `409 Conflict`: Pipeline name already exists

---

### PUT /api/pipelines/{id}

Updates an existing pipeline.

**URL Parameters:**
- `id` (string, required): Pipeline ID

**Request Body:**
Same as POST /api/pipelines (all fields optional for partial update).

**Important Notes:**
- Cannot update a pipeline while it's running (`status` = "Running")
- Field mappings replacement: sending new `fieldMappings` array replaces all existing mappings
- Schedule updates: changing schedule doesn't affect already-scheduled runs

**Success Response (200):**
Returns the updated pipeline object.

**Error Responses:**
- `404 Not Found`: Pipeline not found
- `400 Bad Request`: Validation error or pipeline is running
- `409 Conflict`: Pipeline name conflict

---

### DELETE /api/pipelines/{id}

Deletes a pipeline.

**URL Parameters:**
- `id` (string, required): Pipeline ID

**Important Notes:**
- Cannot delete a pipeline while it's running
- Deleting a pipeline also deletes all its execution history

**Success Response (200):**
```json
{
  "success": true,
  "executionsDeleted": 15  // Number of executions deleted
}
```

**Error Responses:**
- `404 Not Found`: Pipeline not found
- `400 Bad Request`: Pipeline is currently running

---

### POST /api/pipelines/{id}/execute

Executes a pipeline immediately (manual execution).

**URL Parameters:**
- `id` (string, required): Pipeline ID

**Request Body:**
```json
{
  "options": {
    "skipValidation": false,  // Skip pre-execution validation
    "dryRun": false  // Validate and simulate without writing to destination
  }
}
```

**Success Response (200):**
```json
{
  "executionId": "string",
  "pipelineId": "string",
  "pipelineName": "string",
  "status": "Running",
  "startTime": "2025-11-19T03:44:49-05:00",
  "endTime": null,
  "recordsProcessed": 0,
  "errors": []
}
```

**Error Responses:**
- `404 Not Found`: Pipeline not found
- `400 Bad Request`: 
  - Pipeline already running
  - Validation failed (if skipValidation = false)
  - Source/destination connector not accessible
- `409 Conflict`: Pipeline already has a running execution

---

### POST /api/pipelines/validate

Validates a pipeline configuration without saving it.

**Request Body:**
Pipeline object (same as POST /api/pipelines).

**Success Response (200):**
```json
{
  "isValid": true,
  "errors": [],
  "warnings": [
    {
      "field": "fieldMappings[2].destinationField",
      "message": "Field 'email' not found in destination schema",
      "severity": "warning"
    }
  ],
  "stats": {
    "totalFieldMappings": 5,
    "mappingsWithTransformations": 3,
    "unmappedSourceFields": ["phone", "address"],
    "unmappedDestinationFields": ["created_at"]
  }
}
```

**Error Response (400):**
```json
{
  "isValid": false,
  "errors": [
    {
      "field": "sourceId",
      "message": "Source connector does not exist",
      "code": "PIPE_001"
    },
    {
      "field": "fieldMappings[0].sourceFields[0]",
      "message": "Source field 'InvalidField' not found in connector schema",
      "code": "PIPE_003"
    }
  ]
}
```

---

### GET /api/pipelines/connector/{connectorId}

Finds all pipelines that use a specific connector (as source or destination).

**URL Parameters:**
- `connectorId` (string, required): Connector ID

**Success Response (200):**
```json
[
  {
    "id": "string",
    "name": "string",
    "description": "string",
    "usedAs": "source|destination",
    "status": "Idle|Running|Failed"
  }
]
```

---

## Field Mappings Structure

Field mappings define how data flows from source to destination.

### Basic Mapping (One-to-One)
```json
{
  "id": "1",
  "sourceFields": ["OrderId"],
  "destinationField": "SaleId",
  "transformations": []
}
```

### Mapping with Transformation
```json
{
  "id": "2",
  "sourceFields": ["TotalAmount"],
  "destinationField": "Amount",
  "transformations": [
    {
      "transformationId": "1"  // References a transformation by ID
    }
  ]
}
```

### Multi-Field Concatenation
```json
{
  "id": "3",
  "sourceFields": ["first_name", "last_name"],
  "destinationField": "full_name",
  "transformations": [
    {
      "transformationId": "7"  // Custom script to concatenate
    }
  ]
}
```

### Multiple Transformations Chain
```json
{
  "id": "4",
  "sourceFields": ["email"],
  "destinationField": "email",
  "transformations": [
    {
      "transformationId": "4"  // Trim whitespace
    },
    {
      "transformationId": "5"  // Convert to lowercase
    }
  ]
}
```

---

## Schedule Configuration

### Daily Schedule
```json
{
  "frequency": "Daily",
  "time": "02:00",
  "cronExpression": "0 2 * * *",
  "timezone": "UTC"
}
```

### Weekly Schedule
```json
{
  "frequency": "Weekly",
  "time": "04:30",
  "dayOfWeek": 1,  // Monday
  "cronExpression": "30 4 * * 1",
  "timezone": "America/New_York"
}
```

### Monthly Schedule
```json
{
  "frequency": "Monthly",
  "time": "00:00",
  "dayOfMonth": 1,  // First day of month
  "cronExpression": "0 0 1 * *",
  "timezone": "America/Los_Angeles"
}
```

### Custom Cron Schedule
```json
{
  "frequency": "Daily",  // Used for display purposes
  "time": "02:00",
  "cronExpression": "0 2,14 * * *",  // Runs at 2am and 2pm daily
  "timezone": "UTC"
}
```

---

## Examples

### Example 1: Simple Data Migration Pipeline

```json
{
  "name": "Sales Data ETL",
  "description": "Extract sales data from SQL Server, transform, and load to data warehouse",
  "sourceId": "1",
  "destinationId": "5",
  "fieldMappings": [
    {
      "sourceFields": ["OrderId"],
      "destinationField": "SaleId",
      "transformations": []
    },
    {
      "sourceFields": ["CustomerId"],
      "destinationField": "ProductId",
      "transformations": []
    },
    {
      "sourceFields": ["OrderDate"],
      "destinationField": "SaleDate",
      "transformations": []
    },
    {
      "sourceFields": ["TotalAmount"],
      "destinationField": "Amount",
      "transformations": [
        {"transformationId": "1"}  // Filter: amount > 1000
      ]
    }
  ],
  "transformationIds": ["1", "4"],
  "isScheduled": true,
  "schedule": {
    "frequency": "Daily",
    "time": "02:00",
    "cronExpression": "0 2 * * *",
    "timezone": "UTC"
  }
}
```

### Example 2: Customer Import with Data Cleansing

```json
{
  "name": "Customer Import",
  "description": "Import customer data from CSV files with data cleansing",
  "sourceId": "2",
  "destinationId": "6",
  "fieldMappings": [
    {
      "sourceFields": ["customer_id"],
      "destinationField": "customer_id",
      "transformations": []
    },
    {
      "sourceFields": ["first_name"],
      "destinationField": "first_name",
      "transformations": [
        {"transformationId": "4"}  // Trim whitespace
      ]
    },
    {
      "sourceFields": ["last_name"],
      "destinationField": "last_name",
      "transformations": [
        {"transformationId": "4"}  // Trim whitespace
      ]
    },
    {
      "sourceFields": ["email"],
      "destinationField": "email",
      "transformations": [
        {"transformationId": "5"}  // Convert to lowercase
      ]
    }
  ],
  "transformationIds": ["2", "3"],
  "isScheduled": true,
  "schedule": {
    "frequency": "Weekly",
    "time": "04:30",
    "dayOfWeek": 1,
    "cronExpression": "30 4 * * 1",
    "timezone": "America/New_York"
  }
}
```

---

## Validation Rules

### Name
- Required
- 3-100 characters
- Must be unique per tenant

### Source and Destination
- Required
- Must be valid connector IDs
- Source connector direction must be "source" or "both"
- Destination connector direction must be "destination" or "both"
- Source and destination cannot be the same connector

### Field Mappings
- At least one field mapping required
- sourceFields: Required, non-empty array
- destinationField: Required, must exist in destination connector schema
- transformationId: Must reference existing transformation
- Source fields must exist in source connector schema

### Schedule
- Required if isScheduled = true
- frequency: Required
- time: Required, format "HH:mm" (24-hour)
- dayOfWeek: Required if frequency = "Weekly" (1-7)
- dayOfMonth: Required if frequency = "Monthly" (1-31)
- timezone: Required, valid IANA timezone identifier

---

## Status Values

- `Idle` - Pipeline is not running, ready to execute
- `Running` - Pipeline is currently being executed
- `Failed` - Last execution failed (pipeline can still be run again)

**Note:** Status represents the current state, not historical execution results.

---

## Error Codes

- `PIPE_001`: Pipeline not found
- `PIPE_002`: Source connector not found or invalid
- `PIPE_003`: Destination connector not found or invalid
- `PIPE_004`: Field mapping validation failed
- `PIPE_005`: Source field not found in connector schema
- `PIPE_006`: Destination field not found in connector schema
- `PIPE_007`: Transformation not found
- `PIPE_008`: Pipeline is currently running
- `PIPE_009`: Invalid schedule configuration
- `PIPE_010`: Cannot use same connector as source and destination
- `PIPE_011`: Connector direction mismatch
