# Dashboard API Specification

**Resource:** Dashboard  
**Base Path:** `/api/dashboard`  
**Version:** 2.0  
**Last Updated:** 2025-11-19

---

## Overview

The Dashboard API provides aggregated statistics and recent activity data for the main dashboard view.

---

## Endpoints

### GET /api/dashboard

Gets dashboard statistics and recent execution data.

**Success Response (200):**
```json
{
  "stats": {
    "totalPipelines": 5,
    "activePipelines": 1,
    "connectors": 18,
    "recentExecutions": 3
  },
  "statusDistribution": [
    {
      "name": "Completed",
      "count": 1
    },
    {
      "name": "Running",
      "count": 1
    },
    {
      "name": "Failed",
      "count": 1
    }
  ],
  "recentExecutions": [
    {
      "id": "string",
      "pipelineName": "string",
      "startTime": "2025-11-19T03:44:49-05:00",
      "duration": 300000,  // milliseconds
      "status": "Completed|Running|Failed",
      "rowsProcessed": 12345
    }
  ]
}
```

---

## Response Fields

### stats
- `totalPipelines` - Total number of pipelines (all statuses)
- `activePipelines` - Number of pipelines currently running
- `connectors` - Total number of configured connectors
- `recentExecutions` - Number of executions in the last 24 hours

### statusDistribution
Array of execution status counts for visualization (pie/donut charts).

### recentExecutions
Last 5 pipeline executions, sorted by `startTime` descending (most recent first).

---

## Example

```json
{
  "stats": {
    "totalPipelines": 5,
    "activePipelines": 1,
    "connectors": 18,
    "recentExecutions": 3
  },
  "statusDistribution": [
    {"name": "Completed", "count": 1},
    {"name": "Running", "count": 1},
    {"name": "Failed", "count": 1}
  ],
  "recentExecutions": [
    {
      "id": "2",
      "pipelineName": "Customer Import",
      "startTime": "2025-11-19T03:30:00Z",
      "duration": null,
      "status": "Running",
      "rowsProcessed": 5000
    },
    {
      "id": "1",
      "pipelineName": "Sales Data ETL",
      "startTime": "2025-11-17T02:00:00Z",
      "duration": 300000,
      "status": "Completed",
      "rowsProcessed": 12345
    },
    {
      "id": "3",
      "pipelineName": "Product Sync",
      "startTime": "2025-11-19T00:00:00Z",
      "duration": 180000,
      "status": "Failed",
      "rowsProcessed": 0
    }
  ]
}
```
