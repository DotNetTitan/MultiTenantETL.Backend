# SignalR Real-Time Updates - UI Features

## Connection Status Indicator

The ExecutionsView now displays a connection status indicator at the top of the page:

```
┌─────────────────────────────────────────────────────────────────┐
│  Executions    [🟢 Live Updates]              [🔄 Refresh]      │
└─────────────────────────────────────────────────────────────────┘
```

When connected:
- Shows green chip with "Live Updates" text and Wi-Fi icon
- Indicates SignalR is connected and receiving real-time updates

When disconnected:
- Shows grey chip with "Offline" text and Wi-Fi off icon
- Indicates connection is unavailable (user can still manually refresh)

## Real-Time Features

### 1. Execution List Updates

The execution list automatically updates when:
- New executions are started (appear immediately)
- Status changes (Queued → Running → Completed/Failed/Cancelled)
- Progress updates (records processed count increases)
- Executions complete (final status and duration shown)

**Before (polling every 30s):**
- User had to wait up to 30 seconds to see updates
- Manual refresh button required for immediate updates
- Inefficient server load from constant polling

**After (SignalR real-time):**
- Updates appear instantly when events occur
- No polling overhead
- More responsive user experience

### 2. Progress Tracking

When viewing an execution in the list:
- Progress percentage updates after each batch
- Records processed count increases in real-time
- Duration updates continuously for running executions

### 3. Live Log Streaming

When viewing execution details dialog:
- Logs appear in real-time as they're generated
- Auto-scroll to latest log entry
- Color-coded by severity (Info, Warning, Error)
- No need to refresh to see new logs

**Log Viewer:**
```
┌─────────────────────────────────────────────────────────────┐
│  📄 Execution Logs                              [Copy]       │
├─────────────────────────────────────────────────────────────┤
│  [2026-02-04 08:15:01] Pipeline execution started           │
│  [2026-02-04 08:15:02] Batch 1: Read 1000 rows from source  │
│  [2026-02-04 08:15:03] Batch 1: Applied field mappings      │
│  [2026-02-04 08:15:04] Batch 1: Writing to destination      │
│  [2026-02-04 08:15:05] Batch 1 completed: 1000 rows ✓       │
│  ↓ New logs appear here automatically ↓                     │
└─────────────────────────────────────────────────────────────┘
```

### 4. Status Notifications

Toast notifications appear for important events:
- ✅ "Execution completed successfully" (green)
- ❌ "Execution failed" (red)
- ⚠️ "Execution cancelled" (orange)

## Technical Implementation

### Service Layer
```javascript
// executionHubService.js
- Manages SignalR connection
- Handles reconnection with exponential backoff
- Dispatches events to registered handlers
```

### Composable Layer
```javascript
// useExecutionHub.js
- Vue-friendly API
- Reactive connection status
- Auto-cleanup on unmount
```

### Component Integration
```javascript
// ExecutionsView.vue
const { isConnected, onStatusChanged, onProgressUpdated, onLogAdded } = useExecutionHub()

onStatusChanged((update) => {
  // Update execution in list
  // Show notification
})

onProgressUpdated((update) => {
  // Update progress bar
  // Update metrics
})

onLogAdded((data) => {
  // Append log to viewer
  // Auto-scroll
})
```

## Benefits

1. **Better UX**: Users see changes immediately
2. **Reduced Load**: No constant polling overhead
3. **Real-time Feedback**: Know exactly when things complete
4. **Live Monitoring**: Watch executions progress in real-time
5. **Efficient**: Server-push model is more efficient than polling

## Browser Compatibility

SignalR is compatible with all modern browsers:
- Chrome/Edge (Chromium)
- Firefox
- Safari
- Opera

Falls back to long-polling if WebSockets unavailable.
