# Frontend SignalR Integration

This document describes the SignalR integration in the Vue 3 frontend for real-time execution updates.

## Overview

The frontend now connects to the SignalR hub at `/hubs/executions` to receive real-time updates about pipeline executions. This provides a live view of execution status, progress, and logs without requiring manual page refreshes.

## Architecture

### Service Layer (`executionHubService.js`)

The `ExecutionHubService` is a singleton service that manages the SignalR connection:

- **Automatic reconnection** with exponential backoff (0s, 2s, 10s, 30s, 60s)
- **Event handling** for three types of updates:
  - `ExecutionStatusChanged` - Status transitions
  - `ExecutionProgressUpdated` - Progress metrics
  - `ExecutionLogAdded` - New log entries
- **Connection management** with proper cleanup

### Composable (`useExecutionHub.js`)

The `useExecutionHub` composable provides a Vue-friendly API:

```javascript
import { useExecutionHub } from '@/composables/useExecutionHub'

const { 
  isConnected, 
  connectionError,
  onStatusChanged,
  onProgressUpdated,
  onLogAdded 
} = useExecutionHub()
```

Features:
- Automatic connection on component mount
- Automatic cleanup on component unmount
- Reactive connection status
- Simple event handler registration

## Usage in ExecutionsView

The `ExecutionsView.vue` component demonstrates the integration:

### 1. Connection Status Indicator

A chip indicator shows the real-time connection status:
- **Green "Live Updates"** - Connected and receiving updates
- **Grey "Offline"** - Disconnected

### 2. Execution Status Updates

When an execution status changes (e.g., Running → Completed), the UI automatically updates:
- Execution list is updated
- Status chip changes color
- Notification is shown for completion/failure
- Details dialog is updated if viewing that execution

### 3. Progress Updates

During execution, progress metrics are updated in real-time:
- Records processed count
- Progress percentage
- Batch count

These updates happen after each batch is processed, providing granular progress tracking.

### 4. Log Stream

When viewing execution details, new log entries are streamed in real-time:
- Logs are appended to the log viewer
- Auto-scroll to latest log entry
- Syntax highlighting by log level (Info, Warning, Error)

## Event Handlers

### ExecutionStatusChanged

Triggered when execution status changes:

```javascript
onStatusChanged((update) => {
  // update = {
  //   executionId: "guid",
  //   status: "Running|Completed|Failed|Cancelled",
  //   endTime?: "2026-02-04T08:00:00Z",
  //   duration?: "00:01:23.456",
  //   errorMessage?: "Error details"
  // }
})
```

### ExecutionProgressUpdated

Triggered after each batch is processed:

```javascript
onProgressUpdated((update) => {
  // update = {
  //   executionId: "guid",
  //   recordsProcessed: 1000,
  //   recordsSucceeded: 950,
  //   recordsFailed: 50,
  //   progressPercent: 45,
  //   batchCount: 5
  // }
})
```

### ExecutionLogAdded

Triggered when a new log entry is created:

```javascript
onLogAdded((data) => {
  // data = {
  //   executionId: "guid",
  //   log: {
  //     timestamp: "2026-02-04T08:00:00Z",
  //     level: "Info|Warning|Error",
  //     source: "System|DataReader|DataWriter|FieldMapping|Batch",
  //     message: "Log message",
  //     details?: "Additional details"
  //   }
  // }
})
```

## Configuration

The hub URL is automatically constructed from the API base URL configured in `src/config/api.js`:

```javascript
export const API_CONFIG = {
  baseURL: import.meta.env.VITE_API_BASE_URL || 'http://localhost:5244'
}
```

The hub endpoint is: `${baseURL}/hubs/executions`

## Authentication

The SignalR connection uses the JWT token from localStorage for authentication:

```javascript
.withUrl(hubUrl, {
  accessTokenFactory: () => localStorage.getItem('access_token') || ''
})
```

This ensures only authenticated users can connect, and they only receive updates for their tenant's executions.

## Error Handling

The integration includes robust error handling:

1. **Connection Failures**: Automatic reconnection with exponential backoff
2. **Handler Errors**: Try-catch blocks around event handlers prevent crashes
3. **Network Issues**: Graceful degradation - users can still manually refresh
4. **Token Expiration**: Connection will fail if token expires; user must re-login

## Testing

To test the real-time updates:

1. Start the API server
2. Start the Vue dev server: `npm run dev`
3. Login to the application
4. Navigate to the Executions page
5. Start a pipeline execution
6. Observe real-time updates:
   - Status changes from Queued → Running → Completed
   - Progress percentage increases
   - Logs appear in real-time if viewing details

## Future Enhancements

Potential improvements:

1. **Toast notifications** for status changes (already partially implemented)
2. **Sound alerts** for failures
3. **Desktop notifications** using the Notifications API
4. **Execution cancellation** via SignalR (currently uses HTTP POST)
5. **Bulk operations** with live progress tracking
6. **Connection quality indicator** (latency, packet loss)

## Troubleshooting

**Connection fails immediately:**
- Check API server is running
- Verify hub endpoint `/hubs/executions` is accessible
- Check browser console for detailed errors
- Verify JWT token is valid

**Not receiving updates:**
- Check connection status indicator (should be green)
- Verify user is authenticated
- Check tenant ID matches execution tenant
- Look for errors in browser console

**Logs not appearing in real-time:**
- Verify you're viewing the execution details dialog
- Check logs tab is active
- Look for JavaScript errors in console

**Multiple connections:**
- Ensure component is properly unmounted
- Check for memory leaks in browser DevTools
