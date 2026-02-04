# SignalR Real-Time Log Streaming Integration Guide for Vue.js

This guide explains how to integrate real-time pipeline execution log streaming with a Vue.js Single Page Application (SPA).

## Overview

The MultiTenant ETL API provides real-time log streaming for pipeline executions using SignalR. This allows your Vue.js application to receive live updates as pipeline executions progress, including:

- Real-time log entries (Info, Warning, Error, Debug)
- Execution status updates (progress, records processed, batch count)
- Immediate notifications when execution completes or fails

## Prerequisites

1. Node.js and npm installed
2. A Vue.js 3+ application
3. Authentication tokens from the MultiTenant ETL API

## Installation

Install the SignalR client library:

```bash
npm install @microsoft/signalr
```

## Basic Integration

### 1. Create a SignalR Service

Create a new file `src/services/signalr.service.js`:

```javascript
import * as signalR from '@microsoft/signalr';

class SignalRService {
  constructor() {
    this.connection = null;
    this.isConnected = false;
  }

  /**
   * Initializes the SignalR connection
   * @param {string} accessToken - JWT access token from authentication
   * @param {string} hubUrl - SignalR hub URL (default: /hubs/pipeline-execution)
   */
  async connect(accessToken, hubUrl = '/hubs/pipeline-execution') {
    if (this.connection) {
      await this.disconnect();
    }

    // Build the full URL
    const baseUrl = import.meta.env.VITE_API_URL || 'http://localhost:5000';
    const fullHubUrl = `${baseUrl}${hubUrl}`;

    this.connection = new signalR.HubConnectionBuilder()
      .withUrl(fullHubUrl, {
        accessTokenFactory: () => accessToken,
        withCredentials: true
      })
      .withAutomaticReconnect({
        nextRetryDelayInMilliseconds: (retryContext) => {
          // Exponential backoff: 0, 2, 10, 30 seconds
          if (retryContext.previousRetryCount === 0) {
            return 0;
          }
          if (retryContext.previousRetryCount === 1) {
            return 2000;
          }
          if (retryContext.previousRetryCount === 2) {
            return 10000;
          }
          return 30000;
        }
      })
      .configureLogging(signalR.LogLevel.Information)
      .build();

    // Connection lifecycle handlers
    this.connection.onreconnecting((error) => {
      console.warn('SignalR reconnecting...', error);
      this.isConnected = false;
    });

    this.connection.onreconnected((connectionId) => {
      console.log('SignalR reconnected:', connectionId);
      this.isConnected = true;
    });

    this.connection.onclose((error) => {
      console.error('SignalR connection closed', error);
      this.isConnected = false;
    });

    try {
      await this.connection.start();
      this.isConnected = true;
      console.log('SignalR connected successfully');
    } catch (error) {
      console.error('Error connecting to SignalR:', error);
      throw error;
    }
  }

  /**
   * Disconnects from the SignalR hub
   */
  async disconnect() {
    if (this.connection) {
      try {
        await this.connection.stop();
        this.isConnected = false;
        this.connection = null;
        console.log('SignalR disconnected');
      } catch (error) {
        console.error('Error disconnecting from SignalR:', error);
      }
    }
  }

  /**
   * Subscribes to execution logs for a specific pipeline execution
   * @param {string} executionId - The GUID of the execution
   */
  async subscribeToExecution(executionId) {
    if (!this.connection || !this.isConnected) {
      throw new Error('SignalR is not connected');
    }
    await this.connection.invoke('SubscribeToExecution', executionId);
    console.log(`Subscribed to execution: ${executionId}`);
  }

  /**
   * Unsubscribes from execution logs
   * @param {string} executionId - The GUID of the execution
   */
  async unsubscribeFromExecution(executionId) {
    if (!this.connection || !this.isConnected) {
      return;
    }
    await this.connection.invoke('UnsubscribeFromExecution', executionId);
    console.log(`Unsubscribed from execution: ${executionId}`);
  }

  /**
   * Registers a callback for log entry events
   * @param {function} callback - Function to call when a log entry is received
   */
  onLogEntry(callback) {
    if (!this.connection) {
      throw new Error('SignalR is not connected');
    }
    this.connection.on('ReceiveLogEntry', callback);
  }

  /**
   * Registers a callback for execution status events
   * @param {function} callback - Function to call when status is updated
   */
  onExecutionStatus(callback) {
    if (!this.connection) {
      throw new Error('SignalR is not connected');
    }
    this.connection.on('ReceiveExecutionStatus', callback);
  }

  /**
   * Removes all event handlers
   */
  removeAllHandlers() {
    if (this.connection) {
      this.connection.off('ReceiveLogEntry');
      this.connection.off('ReceiveExecutionStatus');
    }
  }
}

// Export singleton instance
export default new SignalRService();
```

### 2. Create a Vue Composable (Recommended for Vue 3)

Create `src/composables/usePipelineExecution.js`:

```javascript
import { ref, onMounted, onUnmounted } from 'vue';
import signalRService from '@/services/signalr.service';

export function usePipelineExecution(executionId, accessToken) {
  const logs = ref([]);
  const status = ref(null);
  const isConnected = ref(false);
  const error = ref(null);

  // Initialize SignalR connection
  const connect = async () => {
    try {
      await signalRService.connect(accessToken);
      isConnected.value = true;

      // Register event handlers
      signalRService.onLogEntry((logEntry) => {
        logs.value.push({
          id: logEntry.id,
          timestamp: new Date(logEntry.timestamp),
          level: logEntry.level,
          source: logEntry.source,
          message: logEntry.message,
          details: logEntry.details
        });
      });

      signalRService.onExecutionStatus((statusUpdate) => {
        status.value = {
          executionId: statusUpdate.executionId,
          status: statusUpdate.status,
          recordsProcessed: statusUpdate.recordsProcessed,
          recordsSucceeded: statusUpdate.recordsSucceeded,
          recordsFailed: statusUpdate.recordsFailed,
          progressPercent: statusUpdate.progressPercent,
          batchCount: statusUpdate.batchCount
        };
      });

      // Subscribe to the specific execution
      await signalRService.subscribeToExecution(executionId);
    } catch (err) {
      console.error('Failed to connect to SignalR:', err);
      error.value = err.message;
    }
  };

  // Cleanup on unmount
  const disconnect = async () => {
    try {
      if (isConnected.value) {
        await signalRService.unsubscribeFromExecution(executionId);
        signalRService.removeAllHandlers();
        await signalRService.disconnect();
        isConnected.value = false;
      }
    } catch (err) {
      console.error('Error disconnecting:', err);
    }
  };

  onMounted(connect);
  onUnmounted(disconnect);

  return {
    logs,
    status,
    isConnected,
    error,
    connect,
    disconnect
  };
}
```

### 3. Use in a Vue Component

Create a component `src/components/PipelineExecutionMonitor.vue`:

```vue
<template>
  <div class="execution-monitor">
    <div class="status-bar" v-if="status">
      <div class="status-badge" :class="status.status.toLowerCase()">
        {{ status.status }}
      </div>
      <div class="progress-info">
        <div class="progress-bar">
          <div 
            class="progress-fill" 
            :style="{ width: status.progressPercent + '%' }"
          ></div>
        </div>
        <div class="stats">
          <span>Processed: {{ status.recordsProcessed }}</span>
          <span>Succeeded: {{ status.recordsSucceeded }}</span>
          <span>Failed: {{ status.recordsFailed }}</span>
          <span>Batches: {{ status.batchCount }}</span>
          <span>{{ Math.round(status.progressPercent) }}%</span>
        </div>
      </div>
    </div>

    <div class="connection-status">
      <span v-if="isConnected" class="connected">● Connected</span>
      <span v-else class="disconnected">● Disconnected</span>
      <span v-if="error" class="error">Error: {{ error }}</span>
    </div>

    <div class="logs-container">
      <h3>Execution Logs</h3>
      <div class="logs">
        <div 
          v-for="log in logs" 
          :key="log.id" 
          class="log-entry"
          :class="log.level.toLowerCase()"
        >
          <span class="timestamp">{{ formatTime(log.timestamp) }}</span>
          <span class="level">{{ log.level }}</span>
          <span class="source">{{ log.source }}</span>
          <span class="message">{{ log.message }}</span>
          <details v-if="log.details" class="details">
            <summary>Details</summary>
            <pre>{{ log.details }}</pre>
          </details>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { computed } from 'vue';
import { usePipelineExecution } from '@/composables/usePipelineExecution';

const props = defineProps({
  executionId: {
    type: String,
    required: true
  },
  accessToken: {
    type: String,
    required: true
  }
});

const { logs, status, isConnected, error } = usePipelineExecution(
  props.executionId,
  props.accessToken
);

const formatTime = (date) => {
  return date.toLocaleTimeString('en-US', { 
    hour12: false,
    hour: '2-digit',
    minute: '2-digit',
    second: '2-digit'
  });
};
</script>

<style scoped>
.execution-monitor {
  padding: 20px;
  font-family: system-ui, -apple-system, sans-serif;
}

.status-bar {
  margin-bottom: 20px;
  padding: 15px;
  border-radius: 8px;
  background: #f5f5f5;
}

.status-badge {
  display: inline-block;
  padding: 5px 15px;
  border-radius: 20px;
  font-weight: 600;
  margin-bottom: 10px;
}

.status-badge.running {
  background: #3b82f6;
  color: white;
}

.status-badge.completed {
  background: #10b981;
  color: white;
}

.status-badge.failed {
  background: #ef4444;
  color: white;
}

.status-badge.cancelled {
  background: #6b7280;
  color: white;
}

.progress-bar {
  height: 8px;
  background: #e5e7eb;
  border-radius: 4px;
  overflow: hidden;
  margin-bottom: 10px;
}

.progress-fill {
  height: 100%;
  background: #3b82f6;
  transition: width 0.3s ease;
}

.stats {
  display: flex;
  gap: 20px;
  font-size: 14px;
  color: #6b7280;
}

.connection-status {
  margin-bottom: 15px;
  font-size: 14px;
}

.connected {
  color: #10b981;
  font-weight: 600;
}

.disconnected {
  color: #ef4444;
  font-weight: 600;
}

.logs-container {
  border: 1px solid #e5e7eb;
  border-radius: 8px;
  padding: 15px;
  background: white;
}

.logs {
  max-height: 500px;
  overflow-y: auto;
}

.log-entry {
  padding: 10px;
  border-bottom: 1px solid #f3f4f6;
  font-family: 'Courier New', monospace;
  font-size: 13px;
}

.log-entry:last-child {
  border-bottom: none;
}

.log-entry.info {
  background: #f0f9ff;
}

.log-entry.warning {
  background: #fffbeb;
}

.log-entry.error {
  background: #fef2f2;
}

.log-entry.debug {
  background: #f9fafb;
  color: #6b7280;
}

.timestamp {
  color: #6b7280;
  margin-right: 10px;
}

.level {
  font-weight: 600;
  margin-right: 10px;
  text-transform: uppercase;
}

.log-entry.info .level {
  color: #3b82f6;
}

.log-entry.warning .level {
  color: #f59e0b;
}

.log-entry.error .level {
  color: #ef4444;
}

.source {
  color: #8b5cf6;
  margin-right: 10px;
}

.message {
  color: #1f2937;
}

.details summary {
  cursor: pointer;
  margin-top: 5px;
  color: #6b7280;
}

.details pre {
  margin-top: 5px;
  padding: 10px;
  background: #f9fafb;
  border-radius: 4px;
  overflow-x: auto;
}
</style>
```

### 4. Use the Component in Your App

```vue
<template>
  <div id="app">
    <h1>Pipeline Execution Monitor</h1>
    <PipelineExecutionMonitor 
      :execution-id="executionId" 
      :access-token="accessToken"
    />
  </div>
</template>

<script setup>
import { ref } from 'vue';
import PipelineExecutionMonitor from './components/PipelineExecutionMonitor.vue';
import { useAuthStore } from '@/stores/auth'; // Your auth store

const authStore = useAuthStore();
const accessToken = ref(authStore.getAccessToken());
const executionId = ref('your-execution-id-here'); // From route params or API
</script>
```

## Alternative: Using Options API

If you're using Vue 2 or Options API, here's the equivalent:

```vue
<template>
  <!-- Same template as above -->
</template>

<script>
import signalRService from '@/services/signalr.service';

export default {
  name: 'PipelineExecutionMonitor',
  props: {
    executionId: {
      type: String,
      required: true
    },
    accessToken: {
      type: String,
      required: true
    }
  },
  data() {
    return {
      logs: [],
      status: null,
      isConnected: false,
      error: null
    };
  },
  async mounted() {
    try {
      await signalRService.connect(this.accessToken);
      this.isConnected = true;

      signalRService.onLogEntry((logEntry) => {
        this.logs.push({
          id: logEntry.id,
          timestamp: new Date(logEntry.timestamp),
          level: logEntry.level,
          source: logEntry.source,
          message: logEntry.message,
          details: logEntry.details
        });
      });

      signalRService.onExecutionStatus((statusUpdate) => {
        this.status = statusUpdate;
      });

      await signalRService.subscribeToExecution(this.executionId);
    } catch (err) {
      console.error('Failed to connect:', err);
      this.error = err.message;
    }
  },
  async beforeUnmount() {
    try {
      if (this.isConnected) {
        await signalRService.unsubscribeFromExecution(this.executionId);
        signalRService.removeAllHandlers();
        await signalRService.disconnect();
      }
    } catch (err) {
      console.error('Error disconnecting:', err);
    }
  },
  methods: {
    formatTime(date) {
      return date.toLocaleTimeString('en-US', { 
        hour12: false,
        hour: '2-digit',
        minute: '2-digit',
        second: '2-digit'
      });
    }
  }
};
</script>
```

## Environment Configuration

Add to your `.env` file:

```env
VITE_API_URL=http://localhost:5000
```

For production:

```env
VITE_API_URL=https://api.yourcompany.com
```

## API Endpoints

### Get Historical Logs

If you need to load historical logs before connecting to SignalR:

```javascript
async function getExecutionLogs(executionId, accessToken) {
  const response = await fetch(
    `${import.meta.env.VITE_API_URL}/api/executions/${executionId}/logs`,
    {
      headers: {
        'Authorization': `Bearer ${accessToken}`,
        'Content-Type': 'application/json'
      }
    }
  );
  
  if (!response.ok) {
    throw new Error('Failed to fetch logs');
  }
  
  return await response.json();
}
```

## Error Handling

The SignalR service includes automatic reconnection with exponential backoff. Handle connection errors gracefully:

```javascript
try {
  await signalRService.connect(accessToken);
} catch (error) {
  if (error.statusCode === 401) {
    // Token expired - refresh token
    console.error('Authentication failed');
  } else {
    // Other connection errors
    console.error('Connection failed:', error);
  }
}
```

## Performance Tips

1. **Limit Log Display**: Only show the last N logs to prevent memory issues
   ```javascript
   const MAX_LOGS = 1000;
   if (logs.value.length > MAX_LOGS) {
     logs.value = logs.value.slice(-MAX_LOGS);
   }
   ```

2. **Auto-scroll**: Automatically scroll to bottom on new logs
   ```javascript
   import { nextTick } from 'vue';
   
   signalRService.onLogEntry((logEntry) => {
     logs.value.push(logEntry);
     nextTick(() => {
       const logsContainer = document.querySelector('.logs');
       if (logsContainer) {
         logsContainer.scrollTop = logsContainer.scrollHeight;
       }
     });
   });
   ```

3. **Disconnect when not needed**: Always disconnect when navigating away to free resources

## CORS Configuration

Ensure your API allows SignalR connections from your Vue app. The API should have CORS configured with:

```csharp
.AllowCredentials()
.WithOrigins("http://localhost:5173") // Your Vue dev server
```

## Troubleshooting

### Connection Fails

1. Check that the API is running and accessible
2. Verify your access token is valid
3. Check browser console for CORS errors
4. Ensure SignalR hub is mapped correctly in the API

### Not Receiving Events

1. Verify you've called `subscribeToExecution` with the correct execution ID
2. Check that the execution ID matches an active pipeline execution
3. Ensure event handlers are registered before subscribing

### Memory Leaks

1. Always call `disconnect()` in `onUnmounted` or `beforeUnmount`
2. Remove event handlers with `removeAllHandlers()`
3. Limit the number of logs stored in memory

## Support

For issues or questions, please refer to the main MultiTenant ETL documentation or open an issue on the GitHub repository.
