# SignalR Vue 3 Integration Guide

This guide explains how to integrate real-time pipeline execution updates using SignalR in a Vue 3 application.

## Overview

The MultiTenant ETL API provides real-time updates for pipeline executions via SignalR. This allows frontend applications to:

- Stream execution logs in real-time
- Receive live progress updates (records processed, success/failure counts)
- Get instant status change notifications
- Receive completion notifications

## Architecture

The system uses an HTTP callback architecture where:
1. **Worker** processes pipelines and makes HTTP calls to API
2. **API** broadcasts updates via SignalR to connected clients
3. **Vue 3 Client** receives updates in real-time via WebSocket

```
Worker (execute) → HTTP POST → API (broadcast) → SignalR → Vue 3 Client
```

This ensures real-time updates work even though execution happens in a separate Worker process.

## SignalR Hub Endpoint

**Hub URL:** `https://your-api-domain/hubs/executions`

**Authentication:** Bearer token required (JWT access token)

## Prerequisites

Install the SignalR client library for JavaScript:

```bash
npm install @microsoft/signalr
```

## Vue 3 Integration

### 1. Create a SignalR Service

Create a service file `src/services/executionHubService.ts`:

```typescript
import * as signalR from '@microsoft/signalr';

export interface ExecutionLog {
  timestamp: string;
  level: string;
  source: string;
  message: string;
  details?: string;
}

export interface ExecutionStatusUpdate {
  status: string;
  timestamp: string;
}

export interface ExecutionProgressUpdate {
  recordsProcessed: number;
  recordsSucceeded: number;
  recordsFailed: number;
  progressPercent: number;
  batchCount: number;
  timestamp: string;
}

export interface ExecutionCompletionUpdate {
  status: string;
  startTime: string;
  endTime: string;
  duration: string;
  recordsProcessed: number;
  recordsSucceeded: number;
  recordsFailed: number;
  errorMessage?: string;
}

export class ExecutionHubService {
  private connection: signalR.HubConnection | null = null;
  private accessToken: string;
  private apiUrl: string;

  constructor(apiUrl: string, accessToken: string) {
    this.apiUrl = apiUrl;
    this.accessToken = accessToken;
  }

  /**
   * Start the SignalR connection
   */
  async start(): Promise<void> {
    if (this.connection?.state === signalR.HubConnectionState.Connected) {
      console.log('SignalR already connected');
      return;
    }

    this.connection = new signalR.HubConnectionBuilder()
      .withUrl(`${this.apiUrl}/hubs/executions`, {
        accessTokenFactory: () => this.accessToken,
        transport: signalR.HttpTransportType.WebSockets | signalR.HttpTransportType.ServerSentEvents,
      })
      .withAutomaticReconnect({
        nextRetryDelayInMilliseconds: (retryContext) => {
          // Exponential backoff: 2s, 4s, 8s, 16s, 30s (max)
          const delays = [2000, 4000, 8000, 16000, 30000];
          return delays[Math.min(retryContext.previousRetryCount, delays.length - 1)];
        },
      })
      .configureLogging(signalR.LogLevel.Information)
      .build();

    // Connection event handlers
    this.connection.onreconnecting((error) => {
      console.warn('SignalR reconnecting...', error);
    });

    this.connection.onreconnected((connectionId) => {
      console.log('SignalR reconnected:', connectionId);
    });

    this.connection.onclose((error) => {
      console.error('SignalR connection closed:', error);
    });

    try {
      await this.connection.start();
      console.log('SignalR connected successfully');
    } catch (error) {
      console.error('Error starting SignalR connection:', error);
      throw error;
    }
  }

  /**
   * Stop the SignalR connection
   */
  async stop(): Promise<void> {
    if (this.connection) {
      await this.connection.stop();
      this.connection = null;
      console.log('SignalR disconnected');
    }
  }

  /**
   * Subscribe to updates for a specific execution
   */
  async subscribeToExecution(executionId: string): Promise<void> {
    if (!this.connection) {
      throw new Error('SignalR connection not initialized');
    }
    await this.connection.invoke('SubscribeToExecution', executionId);
    console.log(`Subscribed to execution: ${executionId}`);
  }

  /**
   * Unsubscribe from updates for a specific execution
   */
  async unsubscribeFromExecution(executionId: string): Promise<void> {
    if (!this.connection) {
      return;
    }
    await this.connection.invoke('UnsubscribeFromExecution', executionId);
    console.log(`Unsubscribed from execution: ${executionId}`);
  }

  /**
   * Register callback for receiving logs
   */
  onLog(callback: (log: ExecutionLog) => void): void {
    if (!this.connection) {
      throw new Error('SignalR connection not initialized');
    }
    this.connection.on('ReceiveLog', callback);
  }

  /**
   * Register callback for receiving status updates
   */
  onStatusUpdate(callback: (update: ExecutionStatusUpdate) => void): void {
    if (!this.connection) {
      throw new Error('SignalR connection not initialized');
    }
    this.connection.on('ReceiveStatusUpdate', callback);
  }

  /**
   * Register callback for receiving progress updates
   */
  onProgressUpdate(callback: (update: ExecutionProgressUpdate) => void): void {
    if (!this.connection) {
      throw new Error('SignalR connection not initialized');
    }
    this.connection.on('ReceiveProgressUpdate', callback);
  }

  /**
   * Register callback for receiving completion notifications
   */
  onCompletion(callback: (completion: ExecutionCompletionUpdate) => void): void {
    if (!this.connection) {
      throw new Error('SignalR connection not initialized');
    }
    this.connection.on('ReceiveCompletion', callback);
  }

  /**
   * Register callback for tenant-wide execution status changes
   */
  onExecutionStatusChanged(callback: (data: { executionId: string; status: string; timestamp: string }) => void): void {
    if (!this.connection) {
      throw new Error('SignalR connection not initialized');
    }
    this.connection.on('ExecutionStatusChanged', callback);
  }

  /**
   * Register callback for tenant-wide execution completions
   */
  onExecutionCompleted(callback: (data: { executionId: string; status: string; recordsProcessed: number }) => void): void {
    if (!this.connection) {
      throw new Error('SignalR connection not initialized');
    }
    this.connection.on('ExecutionCompleted', callback);
  }

  /**
   * Remove all event handlers
   */
  offAll(): void {
    if (!this.connection) {
      return;
    }
    this.connection.off('ReceiveLog');
    this.connection.off('ReceiveStatusUpdate');
    this.connection.off('ReceiveProgressUpdate');
    this.connection.off('ReceiveCompletion');
    this.connection.off('ExecutionStatusChanged');
    this.connection.off('ExecutionCompleted');
  }

  /**
   * Get connection state
   */
  getState(): signalR.HubConnectionState | null {
    return this.connection?.state ?? null;
  }
}
```

### 2. Use SignalR in a Vue Component (Composition API)

Create a component `src/components/ExecutionMonitor.vue`:

```vue
<template>
  <div class="execution-monitor">
    <div class="status-bar">
      <h2>Execution {{ executionId }}</h2>
      <div class="status-badge" :class="statusClass">{{ status }}</div>
      <div class="connection-status" :class="{ connected: isConnected }">
        {{ isConnected ? 'Connected' : 'Disconnected' }}
      </div>
    </div>

    <div class="progress-section" v-if="progress">
      <div class="progress-bar">
        <div class="progress-fill" :style="{ width: `${progress.progressPercent}%` }"></div>
      </div>
      <div class="progress-stats">
        <span>Progress: {{ progress.progressPercent.toFixed(1) }}%</span>
        <span>Processed: {{ progress.recordsProcessed.toLocaleString() }}</span>
        <span>Succeeded: {{ progress.recordsSucceeded.toLocaleString() }}</span>
        <span>Failed: {{ progress.recordsFailed.toLocaleString() }}</span>
        <span>Batches: {{ progress.batchCount }}</span>
      </div>
    </div>

    <div class="logs-section">
      <h3>Execution Logs</h3>
      <div class="logs-container" ref="logsContainer">
        <div
          v-for="(log, index) in logs"
          :key="index"
          class="log-entry"
          :class="`log-${log.level.toLowerCase()}`"
        >
          <span class="log-timestamp">{{ formatTime(log.timestamp) }}</span>
          <span class="log-level">{{ log.level }}</span>
          <span class="log-source">{{ log.source }}</span>
          <span class="log-message">{{ log.message }}</span>
          <span v-if="log.details" class="log-details">{{ log.details }}</span>
        </div>
      </div>
    </div>

    <div class="completion-section" v-if="completion">
      <h3>Execution Completed</h3>
      <div class="completion-details">
        <p><strong>Status:</strong> {{ completion.status }}</p>
        <p><strong>Duration:</strong> {{ formatDuration(completion.duration) }}</p>
        <p><strong>Records Processed:</strong> {{ completion.recordsProcessed.toLocaleString() }}</p>
        <p><strong>Records Succeeded:</strong> {{ completion.recordsSucceeded.toLocaleString() }}</p>
        <p><strong>Records Failed:</strong> {{ completion.recordsFailed.toLocaleString() }}</p>
        <p v-if="completion.errorMessage" class="error-message">
          <strong>Error:</strong> {{ completion.errorMessage }}
        </p>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, onBeforeUnmount, nextTick, computed } from 'vue';
import { ExecutionHubService, type ExecutionLog, type ExecutionProgressUpdate, type ExecutionCompletionUpdate } from '@/services/executionHubService';

interface Props {
  executionId: string;
  apiUrl: string;
  accessToken: string;
}

const props = defineProps<Props>();

const hubService = ref<ExecutionHubService | null>(null);
const isConnected = ref(false);
const status = ref('Connecting...');
const logs = ref<ExecutionLog[]>([]);
const progress = ref<ExecutionProgressUpdate | null>(null);
const completion = ref<ExecutionCompletionUpdate | null>(null);
const logsContainer = ref<HTMLElement | null>(null);

const statusClass = computed(() => {
  const statusLower = status.value.toLowerCase();
  if (statusLower.includes('running')) return 'status-running';
  if (statusLower.includes('completed')) return 'status-completed';
  if (statusLower.includes('failed')) return 'status-failed';
  if (statusLower.includes('cancelled')) return 'status-cancelled';
  return 'status-default';
});

const formatTime = (timestamp: string) => {
  return new Date(timestamp).toLocaleTimeString();
};

const formatDuration = (duration: string) => {
  // Duration is in TimeSpan format (e.g., "00:05:30.1234567")
  const parts = duration.split(':');
  if (parts.length >= 3) {
    const hours = parseInt(parts[0]);
    const minutes = parseInt(parts[1]);
    const seconds = Math.floor(parseFloat(parts[2]));
    
    const formatted: string[] = [];
    if (hours > 0) formatted.push(`${hours}h`);
    if (minutes > 0) formatted.push(`${minutes}m`);
    formatted.push(`${seconds}s`);
    
    return formatted.join(' ');
  }
  return duration;
};

const scrollLogsToBottom = async () => {
  await nextTick();
  if (logsContainer.value) {
    logsContainer.value.scrollTop = logsContainer.value.scrollHeight;
  }
};

onMounted(async () => {
  try {
    // Initialize SignalR service
    hubService.value = new ExecutionHubService(props.apiUrl, props.accessToken);

    // Register event handlers
    hubService.value.onLog((log) => {
      logs.value.push(log);
      scrollLogsToBottom();
    });

    hubService.value.onStatusUpdate((update) => {
      status.value = update.status;
    });

    hubService.value.onProgressUpdate((update) => {
      progress.value = update;
    });

    hubService.value.onCompletion((comp) => {
      completion.value = comp;
      status.value = comp.status;
    });

    // Start connection
    await hubService.value.start();
    isConnected.value = true;

    // Subscribe to execution updates
    await hubService.value.subscribeToExecution(props.executionId);
  } catch (error) {
    console.error('Error initializing SignalR:', error);
    status.value = 'Connection Error';
  }
});

onBeforeUnmount(async () => {
  if (hubService.value) {
    try {
      await hubService.value.unsubscribeFromExecution(props.executionId);
      hubService.value.offAll();
      await hubService.value.stop();
    } catch (error) {
      console.error('Error cleaning up SignalR:', error);
    }
  }
});
</script>

<style scoped>
.execution-monitor {
  padding: 20px;
  font-family: Arial, sans-serif;
}

.status-bar {
  display: flex;
  align-items: center;
  gap: 15px;
  margin-bottom: 20px;
  padding-bottom: 10px;
  border-bottom: 2px solid #eee;
}

.status-badge {
  padding: 5px 15px;
  border-radius: 20px;
  font-weight: bold;
  font-size: 14px;
}

.status-running {
  background-color: #3498db;
  color: white;
}

.status-completed {
  background-color: #2ecc71;
  color: white;
}

.status-failed {
  background-color: #e74c3c;
  color: white;
}

.status-cancelled {
  background-color: #95a5a6;
  color: white;
}

.status-default {
  background-color: #ecf0f1;
  color: #34495e;
}

.connection-status {
  margin-left: auto;
  padding: 5px 10px;
  border-radius: 5px;
  background-color: #e74c3c;
  color: white;
  font-size: 12px;
}

.connection-status.connected {
  background-color: #2ecc71;
}

.progress-section {
  margin-bottom: 30px;
}

.progress-bar {
  height: 30px;
  background-color: #ecf0f1;
  border-radius: 15px;
  overflow: hidden;
  margin-bottom: 10px;
}

.progress-fill {
  height: 100%;
  background: linear-gradient(90deg, #3498db, #2ecc71);
  transition: width 0.3s ease;
}

.progress-stats {
  display: flex;
  gap: 20px;
  font-size: 14px;
  color: #7f8c8d;
}

.logs-section h3 {
  margin-bottom: 10px;
}

.logs-container {
  max-height: 400px;
  overflow-y: auto;
  border: 1px solid #ddd;
  border-radius: 5px;
  padding: 10px;
  background-color: #f8f9fa;
  font-family: 'Courier New', monospace;
  font-size: 13px;
}

.log-entry {
  padding: 5px;
  margin-bottom: 5px;
  border-radius: 3px;
  display: grid;
  grid-template-columns: 90px 70px 120px 1fr;
  gap: 10px;
}

.log-info {
  background-color: #d1ecf1;
}

.log-warning {
  background-color: #fff3cd;
}

.log-error {
  background-color: #f8d7da;
}

.log-debug {
  background-color: #e2e3e5;
}

.log-timestamp {
  color: #6c757d;
}

.log-level {
  font-weight: bold;
}

.log-source {
  color: #007bff;
}

.log-message {
  grid-column: span 4;
}

.log-details {
  grid-column: span 4;
  color: #6c757d;
  font-style: italic;
  margin-top: 5px;
}

.completion-section {
  margin-top: 30px;
  padding: 20px;
  background-color: #f8f9fa;
  border-radius: 10px;
  border: 2px solid #2ecc71;
}

.completion-details p {
  margin: 10px 0;
}

.error-message {
  color: #e74c3c;
  background-color: #fadbd8;
  padding: 10px;
  border-radius: 5px;
}
</style>
```

### 3. Use the Component in Your App

```vue
<template>
  <div id="app">
    <ExecutionMonitor
      :executionId="currentExecutionId"
      :apiUrl="apiUrl"
      :accessToken="accessToken"
    />
  </div>
</template>

<script setup lang="ts">
import { ref } from 'vue';
import ExecutionMonitor from './components/ExecutionMonitor.vue';

const apiUrl = ref('https://your-api-domain');
const accessToken = ref('your-jwt-access-token');
const currentExecutionId = ref('execution-guid-here');
</script>
```

## Events Summary

### Execution-Specific Events

These events are sent to clients subscribed to a specific execution:

1. **ReceiveLog** - Real-time log entries
   ```typescript
   {
     timestamp: string;
     level: string;      // Info, Warning, Error, Debug
     source: string;     // System, DataReader, DataWriter, etc.
     message: string;
     details?: string;
   }
   ```

2. **ReceiveStatusUpdate** - Execution status changes
   ```typescript
   {
     status: string;     // Queued, Running, Completed, Failed, Cancelled
     timestamp: string;
   }
   ```

3. **ReceiveProgressUpdate** - Progress and statistics updates
   ```typescript
   {
     recordsProcessed: number;
     recordsSucceeded: number;
     recordsFailed: number;
     progressPercent: number;
     batchCount: number;
     timestamp: string;
   }
   ```

4. **ReceiveCompletion** - Final completion notification
   ```typescript
   {
     status: string;
     startTime: string;
     endTime: string;
     duration: string;
     recordsProcessed: number;
     recordsSucceeded: number;
     recordsFailed: number;
     errorMessage?: string;
   }
   ```

### Tenant-Wide Events

These events are broadcast to all clients connected to a tenant:

1. **ExecutionStatusChanged** - Notification of any execution status change
   ```typescript
   {
     executionId: string;
     status: string;
     timestamp: string;
   }
   ```

2. **ExecutionCompleted** - Notification when any execution completes
   ```typescript
   {
     executionId: string;
     status: string;
     recordsProcessed: number;
   }
   ```

## Best Practices

1. **Connection Management**
   - Start the connection when mounting components that need real-time updates
   - Always stop the connection when unmounting to free resources
   - Use automatic reconnection to handle network interruptions

2. **Error Handling**
   - Wrap SignalR operations in try-catch blocks
   - Provide user feedback for connection errors
   - Log errors for debugging

3. **Performance**
   - Subscribe only to executions you're actively monitoring
   - Unsubscribe when navigating away
   - Consider debouncing rapid progress updates if needed

4. **Authentication**
   - Always provide a valid JWT access token
   - Refresh tokens before they expire
   - Reconnect with new token after refresh

5. **Testing**
   - Test connection resilience by simulating network interruptions
   - Verify behavior when the token expires
   - Test with multiple concurrent executions

## Troubleshooting

### Connection Issues

**Problem:** Connection fails to establish
- Verify the API URL is correct
- Check that the access token is valid and not expired
- Ensure CORS is configured correctly on the API
- Check browser console for detailed error messages

**Problem:** Connection drops frequently
- Check network stability
- Verify the API server is running
- Increase reconnection delays if needed

### Data Issues

**Problem:** Not receiving updates
- Verify you're subscribed to the correct execution ID
- Check that the execution is actually running
- Ensure event handlers are registered before starting connection

**Problem:** Duplicate messages
- Avoid registering the same event handler multiple times
- Clean up handlers in `onBeforeUnmount`

## Additional Resources

- [SignalR JavaScript Client Documentation](https://docs.microsoft.com/en-us/aspnet/core/signalr/javascript-client)
- [Vue 3 Composition API](https://vuejs.org/guide/extras/composition-api-faq.html)
- MultiTenant ETL API Documentation: `/swagger`

## Support

For issues or questions:
- Check the API Swagger documentation at `https://your-api-domain/swagger`
- Review the browser console for SignalR connection logs
- Contact your system administrator
