# Schedules API Integration

This document describes the frontend integration with the backend Schedules API for managing pipeline execution schedules.

## Overview

The Schedules feature allows users to:
- Create, update, and delete schedules for pipelines
- Enable/disable schedules
- Trigger immediate execution of a schedule
- Validate cron expressions with human-readable descriptions

## Backend Endpoints

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/schedules` | Get all schedules with pagination and filters |
| GET | `/api/schedules/{id}` | Get schedule by ID |
| GET | `/api/schedules/pipeline/{pipelineId}` | Get schedule for a specific pipeline |
| POST | `/api/schedules` | Create a new schedule |
| PUT | `/api/schedules/{id}` | Update an existing schedule |
| DELETE | `/api/schedules/{id}` | Delete a schedule |
| POST | `/api/schedules/{id}/enable` | Enable a schedule |
| POST | `/api/schedules/{id}/disable` | Disable a schedule |
| POST | `/api/schedules/{id}/trigger` | Trigger immediate execution |
| POST | `/api/schedules/validate-cron` | Validate a cron expression |

## Frontend Files

### Services

- `src/services/scheduleService.js` - API service for schedule operations

### Composables

- `src/composables/useSchedule.js` - State management and helper functions

### Views

- `src/views/SchedulesView.vue` - Main schedules management page

### Configuration

- `src/config/api.js` - API endpoint configuration (updated with schedule endpoints)
- `src/locales/en.json` - Translations for schedule-related UI

## Data Models

### Schedule Response
```javascript
{
  id: string,              // Schedule UUID
  pipelineId: string,      // Associated pipeline UUID
  pipelineName: string,    // Pipeline display name
  tenantId: string,        // Tenant UUID
  cronExpression: string,  // Cron expression (e.g., "0 0 * * *")
  timezone: string,        // Timezone (e.g., "UTC", "America/New_York")
  description: string,     // Optional description
  isActive: boolean,       // Whether the schedule is active
  nextRunAt: string,       // ISO timestamp of next scheduled run
  lastRunAt: string,       // ISO timestamp of last run
  lastRunStatus: string,   // Status of last run (Completed, Failed, etc.)
  consecutiveFailures: number,
  maxConsecutiveFailures: number,
  cronDescription: string, // Human-readable cron description
  createdAt: string,
  updatedAt: string
}
```

### Create Schedule Request
```javascript
{
  pipelineId: string,      // Required: Pipeline UUID
  cronExpression: string,  // Required: Valid cron expression
  timezone: string,        // Required: Valid timezone
  description: string,     // Optional: Description
  isActive: boolean        // Optional: Default true
}
```

### Update Schedule Request
```javascript
{
  cronExpression: string,  // Required: Valid cron expression
  timezone: string,        // Required: Valid timezone
  description: string,     // Optional: Description
  isActive: boolean        // Optional
}
```

### Cron Validation Result
```javascript
{
  isValid: boolean,
  errorMessage: string,    // Error message if invalid
  description: string,     // Human-readable description
  nextExecutions: string[] // Array of next 5 execution ISO timestamps
}
```

## Usage Examples

### Using the Service Directly

```javascript
import { scheduleService } from '@/services/scheduleService'

// Get all schedules
const schedules = await scheduleService.getAll({ page: 1, pageSize: 20 })

// Create a schedule
const newSchedule = await scheduleService.create({
  pipelineId: 'pipeline-uuid',
  cronExpression: '0 0 * * *',
  timezone: 'UTC',
  description: 'Daily midnight sync'
})

// Validate a cron expression
const validation = await scheduleService.validateCron('0 */2 * * *', 'UTC')
```

### Using the Composable

```javascript
import { useSchedule } from '@/composables/useSchedule'

const {
  schedules,
  loading,
  loadSchedules,
  saveNewSchedule,
  validateCron,
  cronPresets,
  commonTimezones
} = useSchedule()

// Load schedules
await loadSchedules({ isActive: true })

// Validate before saving
const validation = await validateCron('0 0 * * *', 'UTC')
if (validation.isValid) {
  await saveNewSchedule({
    pipelineId: 'pipeline-uuid',
    cronExpression: '0 0 * * *',
    timezone: 'UTC'
  })
}
```

## Cron Expression Format

The cron expression follows the standard 5-field format:

```
┌───────────── minute (0 - 59)
│ ┌───────────── hour (0 - 23)
│ │ ┌───────────── day of month (1 - 31)
│ │ │ ┌───────────── month (1 - 12)
│ │ │ │ ┌───────────── day of week (0 - 6, Sunday = 0)
│ │ │ │ │
* * * * *
```

### Common Examples

| Expression | Description |
|------------|-------------|
| `* * * * *` | Every minute |
| `0 * * * *` | Every hour |
| `0 0 * * *` | Daily at midnight |
| `0 6 * * *` | Daily at 6 AM |
| `0 0 * * 1` | Weekly on Monday at midnight |
| `0 0 1 * *` | First day of every month at midnight |
| `*/15 * * * *` | Every 15 minutes |

## Supported Timezones

The composable provides a list of common timezones:
- UTC
- America/New_York (Eastern Time)
- America/Chicago (Central Time)
- America/Denver (Mountain Time)
- America/Los_Angeles (Pacific Time)
- Europe/London
- Europe/Paris
- Europe/Berlin
- Asia/Tokyo
- Australia/Sydney
- And more...

## Permissions

The schedule operations require the following permissions:
- `Pipelines.Read` - View schedules
- `Pipelines.Update` - Create, update, delete, enable, disable schedules
- `Pipelines.Execute` - Trigger schedule execution
