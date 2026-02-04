# Pipeline Executions API Specification

**Resource:** Pipeline Executions  
**Base Path:** `/api/executions`  
**Version:** 2.0  
**Last Updated:** 2025-11-19

---

## Overview

Executions represent individual runs of pipelines, tracking progress, status, logs, and results. Each execution has a complete audit trail from start to finish.

### Execution Statuses
- **Running** - Execution is currently in progress
- **Completed** - Execution finished successfully
- **Failed** - Execution encountered errors and stopped

> **Backend Implementation Note:**  
> The API returns status display names (`"Running"`, `"Completed"`, `"Failed"`), but stores codes (`"running"`, `"completed"`, `"failed"`) in lookup tables for consistency and i18n support. See [Database Implementation](./database-implementation.md#statuses).

---

## Endpoints

### GET /api/executions

Gets all pipeline executions with optional filtering.

**Query Parameters:**
- `pipelineId` (string, optional): Filter by specific pipeline ID
- `status` (string, optional): Filter by status - "Running", "Completed", "Failed", "All" (default: "All")
- `startDate` (string, optional): Filter by minimum start date (ISO 8601 format)
- `endDate` (string, optional): Filter by maximum start date (ISO 8601 format)
- `sort` (string, optional): Sort by startTime (default: desc)
  - `startTime_asc` | `startTime_desc`

**Success Response (200):**
```json
[
  {
    "id": "string",
    "pipelineId": "string",
    "pipelineName": "string",
    "status": "Running|Completed|Failed",
    "startTime": "2025-11-19T03:44:49-05:00",
    "endTime": "2025-11-19T03:49:49-05:00",  // null if still running
    "duration": 300000,  // milliseconds, null if still running
    "recordsProcessed": 12345,
    "errors": ["string"],  // Array of error messages
    "progressPercent": 100  // 0-100, estimated progress
  }
]
```

---

### GET /api/executions/{id}

Gets detailed information about a specific execution including logs.

**URL Parameters:**
- `id` (string, required): Execution ID

**Success Response (200):**
```json
{
  "id": "string",
  "pipelineId": "string",
  "pipelineName": "string",
  "status": "Running|Completed|Failed",
  "startTime": "2025-11-19T03:44:49-05:00",
  "endTime": "2025-11-19T03:49:49-05:00",
  "duration": 300000,
  "recordsProcessed": 12345,
  "errors": [
    "Connection timeout: Unable to connect to E-commerce Platform API",
    "Retry attempts exhausted"
  ],
  "progressPercent": 100,
  "logs": [
    {
      "timestamp": "2025-11-19T03:44:49-05:00",
      "level": "INFO|WARN|ERROR",
      "message": "string"
    }
  ]
}
```

**Error Responses:**
- `404 Not Found`: Execution not found

---

### GET /api/executions/pipeline/{pipelineId}

Gets all executions for a specific pipeline.

**URL Parameters:**
- `pipelineId` (string, required): Pipeline ID

**Query Parameters:**
- `limit` (number, optional): Maximum number of executions to return (default: 50)
- `status` (string, optional): Filter by status

**Success Response (200):**
Returns array of execution objects for the specified pipeline, sorted by startTime descending (most recent first).

---

### DELETE /api/executions/{id}

Deletes an execution record.

**URL Parameters:**
- `id` (string, required): Execution ID

**Important Notes:**
- Cannot delete a running execution (must cancel first)
- Deleting execution only removes the record, doesn't affect pipeline

**Success Response (200):**
```json
{
  "success": true
}
```

**Error Responses:**
- `404 Not Found`: Execution not found
- `400 Bad Request`: Cannot delete running execution

---

### POST /api/executions/{id}/cancel

Cancels a running execution.

**URL Parameters:**
- `id` (string, required): Execution ID

**Success Response (200):**
```json
{
  "id": "string",
  "status": "Failed",
  "endTime": "2025-11-19T03:49:49-05:00",
  "errors": ["Execution cancelled by user"],
  "recordsProcessed": 5000  // Records processed before cancellation
}
```

**Error Responses:**
- `404 Not Found`: Execution not found
- `400 Bad Request`: Execution is not running (already completed or failed)

---

### GET /api/executions/{id}/logs

Gets execution logs with filtering and pagination.

**URL Parameters:**
- `id` (string, required): Execution ID

**Query Parameters:**
- `level` (string, optional): Filter by log level - "INFO", "WARN", "ERROR", "All" (default: "All")
- `limit` (number, optional): Maximum logs to return (default: 100)
- `offset` (number, optional): Pagination offset (default: 0)

**Success Response (200):**
```json
{
  "logs": [
    {
      "timestamp": "2025-11-19T03:44:49-05:00",
      "level": "INFO",
      "message": "Pipeline execution started"
    },
    {
      "timestamp": "2025-11-19T03:45:00-05:00",
      "level": "INFO",
      "message": "Connected to SQL Server - Sales"
    },
    {
      "timestamp": "2025-11-19T03:46:00-05:00",
      "level": "INFO",
      "message": "Extracted 12345 records"
    },
    {
      "timestamp": "2025-11-19T03:47:00-05:00",
      "level": "INFO",
      "message": "Applied transformations"
    },
    {
      "timestamp": "2025-11-19T03:48:00-05:00",
      "level": "INFO",
      "message": "Loaded 12345 records to Data Warehouse"
    },
    {
      "timestamp": "2025-11-19T03:49:00-05:00",
      "level": "INFO",
      "message": "Pipeline execution completed successfully"
    }
  ],
  "totalLogs": 6,
  "hasMore": false
}
```

---

### GET /api/executions/{id}/progress (WebSocket/SSE)

**Real-time progress updates** via WebSocket or Server-Sent Events.

**WebSocket URL:**
```
ws://localhost:5000/api/executions/{id}/progress
```

**Message Format:**
```json
{
  "executionId": "string",
  "status": "Running|Completed|Failed",
  "recordsProcessed": 5000,
  "progressPercent": 45,
  "currentStep": "Transforming data",
  "timestamp": "2025-11-19T03:47:30-05:00",
  "log": {
    "timestamp": "2025-11-19T03:47:30-05:00",
    "level": "INFO",
    "message": "Processing batch 5/10"
  }
}
```

**Events:**
- `progress` - Progress update
- `log` - New log entry
- `status_change` - Status changed (Running → Completed/Failed)
- `completed` - Execution completed successfully
- `failed` - Execution failed with errors

---

## Execution Lifecycle

### 1. Initiation
```
POST /api/pipelines/{pipelineId}/execute
→ Returns execution with status="Running", startTime set
```

### 2. Progress Updates
```
GET /api/executions/{executionId}
→ Returns current progress, recordsProcessed, logs

OR

WebSocket /api/executions/{executionId}/progress
→ Real-time updates
```

### 3. Completion
```
Execution finishes:
- status → "Completed" or "Failed"
- endTime set
- duration calculated
- Final recordsProcessed count
- errors array populated (if failed)
```

---

## Log Levels

### INFO
Normal operational messages tracking execution progress:
- Pipeline execution started
- Connected to source/destination
- Extracted N records
- Applied transformations
- Loaded N records
- Pipeline execution completed

### WARN
Warning messages that don't stop execution:
- Retrying connection (attempt 1/3)
- Skipping invalid record
- Schema mismatch detected
- Performance degradation detected

### ERROR
Error messages indicating failures:
- Connection timeout
- Authentication failed
- Transformation failed on record X
- Write operation failed
- Retry attempts exhausted
- Pipeline execution failed

---

## Examples

### Example 1: Successful Execution

```json
{
  "id": "1",
  "pipelineId": "1",
  "pipelineName": "Sales Data ETL",
  "status": "Completed",
  "startTime": "2025-11-17T02:00:00Z",
  "endTime": "2025-11-17T02:05:00Z",
  "duration": 300000,
  "recordsProcessed": 12345,
  "errors": [],
  "progressPercent": 100,
  "logs": [
    {
      "timestamp": "2025-11-17T02:00:00Z",
      "level": "INFO",
      "message": "Pipeline execution started"
    },
    {
      "timestamp": "2025-11-17T02:01:00Z",
      "level": "INFO",
      "message": "Connected to SQL Server - Sales"
    },
    {
      "timestamp": "2025-11-17T02:02:00Z",
      "level": "INFO",
      "message": "Extracted 12345 records"
    },
    {
      "timestamp": "2025-11-17T02:03:00Z",
      "level": "INFO",
      "message": "Applied transformations"
    },
    {
      "timestamp": "2025-11-17T02:04:00Z",
      "level": "INFO",
      "message": "Loaded 12345 records to Data Warehouse"
    },
    {
      "timestamp": "2025-11-17T02:05:00Z",
      "level": "INFO",
      "message": "Pipeline execution completed successfully"
    }
  ]
}
```

### Example 2: Failed Execution

```json
{
  "id": "3",
  "pipelineId": "3",
  "pipelineName": "Product Sync",
  "status": "Failed",
  "startTime": "2025-11-19T00:00:00Z",
  "endTime": "2025-11-19T00:03:00Z",
  "duration": 180000,
  "recordsProcessed": 0,
  "errors": [
    "Connection timeout: Unable to connect to E-commerce Platform API",
    "Retry attempts exhausted"
  ],
  "progressPercent": 0,
  "logs": [
    {
      "timestamp": "2025-11-19T00:00:00Z",
      "level": "INFO",
      "message": "Pipeline execution started"
    },
    {
      "timestamp": "2025-11-19T00:01:00Z",
      "level": "INFO",
      "message": "Connected to ERP API"
    },
    {
      "timestamp": "2025-11-19T00:02:00Z",
      "level": "ERROR",
      "message": "Connection timeout: Unable to connect to E-commerce Platform API"
    },
    {
      "timestamp": "2025-11-19T00:02:30Z",
      "level": "WARN",
      "message": "Retrying connection (attempt 1/3)..."
    },
    {
      "timestamp": "2025-11-19T00:02:42Z",
      "level": "WARN",
      "message": "Retrying connection (attempt 2/3)..."
    },
    {
      "timestamp": "2025-11-19T00:02:54Z",
      "level": "WARN",
      "message": "Retrying connection (attempt 3/3)..."
    },
    {
      "timestamp": "2025-11-19T00:03:00Z",
      "level": "ERROR",
      "message": "Retry attempts exhausted"
    },
    {
      "timestamp": "2025-11-19T00:03:00Z",
      "level": "ERROR",
      "message": "Pipeline execution failed"
    }
  ]
}
```

### Example 3: Running Execution

```json
{
  "id": "2",
  "pipelineId": "2",
  "pipelineName": "Customer Import",
  "status": "Running",
  "startTime": "2025-11-19T03:30:00Z",
  "endTime": null,
  "duration": null,
  "recordsProcessed": 5000,
  "errors": [],
  "progressPercent": 67,
  "logs": [
    {
      "timestamp": "2025-11-19T03:30:00Z",
      "level": "INFO",
      "message": "Pipeline execution started"
    },
    {
      "timestamp": "2025-11-19T03:32:00Z",
      "level": "INFO",
      "message": "Connected to SFTP - Customer Files"
    },
    {
      "timestamp": "2025-11-19T03:35:00Z",
      "level": "INFO",
      "message": "Extracting customer data from CSV"
    },
    {
      "timestamp": "2025-11-19T03:38:00Z",
      "level": "INFO",
      "message": "Processing batch 1/3 - 5000 records processed"
    },
    {
      "timestamp": "2025-11-19T03:42:00Z",
      "level": "INFO",
      "message": "Applying transformations to batch 2/3"
    }
  ]
}
```

---

## Performance Metrics

Executions track several performance metrics:

- **Duration**: Total execution time in milliseconds
- **Records Processed**: Number of records successfully processed
- **Throughput**: Records per second (calculated: recordsProcessed / duration)
- **Error Rate**: Percentage of records that failed processing

These metrics can be used for:
- Performance monitoring
- Capacity planning
- Identifying bottlenecks
- Quality assurance

---

## Error Codes

- `EXEC_001`: Execution not found
- `EXEC_002`: Execution already completed
- `EXEC_003`: Cannot cancel completed execution
- `EXEC_004`: Cannot delete running execution
- `EXEC_005`: Pipeline not found for execution
