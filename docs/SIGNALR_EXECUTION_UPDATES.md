# SignalR Real-Time Execution Updates

This document describes how to connect to and use the SignalR hub for real-time execution updates in the MultiTenant ETL system.

## Overview

The ExecutionHub provides real-time updates for pipeline execution status, progress, and logs. All notifications are scoped to the authenticated user's tenant for security.

## Hub Endpoint

```
/hubs/executions
```

## Authentication

The hub requires authentication. Include the JWT bearer token in your connection:

```javascript
const connection = new signalR.HubConnectionBuilder()
    .withUrl("/hubs/executions", {
        accessTokenFactory: () => getAccessToken() // Your JWT token
    })
    .build();
```

## Events

### ExecutionStatusChanged

Fired when an execution's status changes (e.g., Queued → Running → Completed).

**Payload:**
```json
{
  "executionId": "guid",
  "status": "Running|Completed|Failed|Cancelled",
  "endTime": "2026-02-04T08:00:00Z", // Optional, present when execution completes
  "duration": "00:01:23.456", // Optional, present when execution completes
  "errorMessage": "Error details" // Optional, present when status is Failed
}
```

**Example:**
```javascript
connection.on("ExecutionStatusChanged", (update) => {
    console.log(`Execution ${update.executionId} status: ${update.status}`);
    // Update UI to reflect new status
});
```

### ExecutionProgressUpdated

Fired after each batch is processed during execution, providing progress metrics.

**Payload:**
```json
{
  "executionId": "guid",
  "recordsProcessed": 1000,
  "recordsSucceeded": 950,
  "recordsFailed": 50,
  "progressPercent": 45,
  "batchCount": 5
}
```

**Example:**
```javascript
connection.on("ExecutionProgressUpdated", (update) => {
    console.log(`Progress: ${update.progressPercent}%`);
    console.log(`Processed: ${update.recordsProcessed} records`);
    // Update progress bar
});
```

### ExecutionLogAdded

Fired when a new log entry is added to an execution.

**Payload:**
```json
{
  "executionId": "guid",
  "log": {
    "timestamp": "2026-02-04T08:00:00Z",
    "level": "Info|Warning|Error",
    "source": "System|DataReader|DataWriter|FieldMapping|Batch",
    "message": "Log message",
    "details": "Additional details" // Optional
  }
}
```

**Example:**
```javascript
connection.on("ExecutionLogAdded", (data) => {
    console.log(`[${data.log.level}] ${data.log.message}`);
    // Append to execution log viewer
});
```

## Connection Example

```javascript
import * as signalR from "@microsoft/signalr";

class ExecutionHubClient {
    constructor(baseUrl, getAccessToken) {
        this.connection = new signalR.HubConnectionBuilder()
            .withUrl(`${baseUrl}/hubs/executions`, {
                accessTokenFactory: () => getAccessToken()
            })
            .withAutomaticReconnect()
            .configureLogging(signalR.LogLevel.Information)
            .build();
        
        this.setupHandlers();
    }
    
    setupHandlers() {
        this.connection.on("ExecutionStatusChanged", (update) => {
            this.onStatusChanged?.(update);
        });
        
        this.connection.on("ExecutionProgressUpdated", (update) => {
            this.onProgressUpdated?.(update);
        });
        
        this.connection.on("ExecutionLogAdded", (data) => {
            this.onLogAdded?.(data);
        });
        
        this.connection.onreconnecting(() => {
            console.log("Reconnecting to ExecutionHub...");
        });
        
        this.connection.onreconnected(() => {
            console.log("Reconnected to ExecutionHub");
        });
        
        this.connection.onclose(() => {
            console.log("Connection to ExecutionHub closed");
        });
    }
    
    async start() {
        try {
            await this.connection.start();
            console.log("Connected to ExecutionHub");
        } catch (err) {
            console.error("Failed to connect:", err);
            // Retry logic here
        }
    }
    
    async stop() {
        await this.connection.stop();
    }
}

// Usage
const hubClient = new ExecutionHubClient(
    "https://api.example.com",
    () => localStorage.getItem("access_token")
);

hubClient.onStatusChanged = (update) => {
    // Update execution status in UI
};

hubClient.onProgressUpdated = (update) => {
    // Update progress bar
};

hubClient.onLogAdded = (data) => {
    // Append to log viewer
};

await hubClient.start();
```

## Multi-Tenant Security

- Users are automatically added to their tenant's group on connection
- Only executions belonging to the user's tenant will trigger notifications
- No additional filtering required on the client side

## Best Practices

1. **Reconnection**: Always use `.withAutomaticReconnect()` to handle network interruptions
2. **Error Handling**: Implement proper error handling for connection failures
3. **Cleanup**: Call `stop()` when component unmounts or user logs out
4. **Token Refresh**: Ensure `accessTokenFactory` returns a valid, non-expired token
5. **UI Updates**: Batch UI updates if receiving many rapid notifications

## Testing

To test the SignalR connection without a full execution:

1. Start the API server
2. Authenticate and get a JWT token
3. Connect to the hub using the token
4. Start a pipeline execution via the API
5. Observe real-time notifications in your console/UI

## Troubleshooting

**Connection fails with 401 Unauthorized**
- Verify the JWT token is valid and not expired
- Ensure the token is included in the connection options

**Not receiving notifications**
- Verify you're connected successfully
- Check that you're authenticated as the correct user/tenant
- Ensure event handlers are registered before starting the connection

**Connection drops frequently**
- Enable automatic reconnection
- Check network stability
- Verify server is running and accessible
