<template>
  <v-card>
    <v-card-text>
      <v-row>
        <v-col cols="12" md="6">
          <div class="text-subtitle-2 mb-1">Pipeline</div>
          <div class="d-flex align-center">
            <v-icon icon="mdi-pipe" class="mr-2" color="primary" />
            {{ execution.pipelineName }}
          </div>
        </v-col>
        <v-col cols="12" md="6">
          <div class="text-subtitle-2 mb-1">Status</div>
          <v-chip
            :color="getStatusColor(execution.status)"
            text-color="white"
            :prepend-icon="getStatusIcon(execution.status)"
          >
            {{ execution.status }}
          </v-chip>
        </v-col>
        <v-col cols="12" md="6">
          <div class="text-subtitle-2 mb-1">Start Time</div>
          <div class="d-flex align-center">
            <v-icon icon="mdi-clock" class="mr-2" />
            {{ formatDate(execution.startTime) }}
          </div>
        </v-col>
        <v-col cols="12" md="6">
          <div class="text-subtitle-2 mb-1">Duration</div>
          <div class="d-flex align-center">
            <v-icon icon="mdi-timer" class="mr-2" />
            {{ formatDuration(execution.durationMs) }}
          </div>
        </v-col>
        <v-col cols="12" md="6">
          <div class="text-subtitle-2 mb-1">Rows Processed</div>
          <div class="d-flex align-center">
            <v-icon icon="mdi-database" class="mr-2" />
            {{ execution.rowsProcessed?.toLocaleString() || '-' }}
          </div>
        </v-col>
        <v-col cols="12" md="6">
          <div class="text-subtitle-2 mb-1">Error Rate</div>
          <div class="d-flex align-center">
            <v-icon icon="mdi-alert" class="mr-2" :color="getErrorRateColor(execution.errorRate)" />
            {{ formatErrorRate(execution.errorRate) }}
          </div>
        </v-col>
      </v-row>

      <v-divider class="my-4" />

      <div v-if="execution.logs && execution.logs.length > 0" class="mt-4">
        <div class="text-subtitle-2 mb-2">Execution Log</div>
        <v-card class="bg-grey-darken-4">
          <v-card-text>
            <div class="execution-logs">
              <div
                v-for="(log, index) in execution.logs"
                :key="index"
                class="log-entry"
                :class="`log-${log.level?.toLowerCase()}`"
              >
                <div class="log-row">
                  <div class="log-left">
                    <span class="log-timestamp">{{ formatLogDate(log.timestamp) }}</span>
                  </div>
                  <div class="log-message">{{ formatLogSingleLine(log) }}</div>
                  <div class="log-right">
                    <v-chip size="small" :color="getLogLevelColor(log.level)" text-color="white" class="log-level-chip">{{ log.level }}</v-chip>
                  </div>
                </div>
              </div>
            </div>
          </v-card-text>
        </v-card>
      </div>
    </v-card-text>
  </v-card>
</template>

<script setup>
const props = defineProps({
  execution: {
    type: Object,
    required: true
  }
});

function getStatusColor(status) {
  switch (status?.toLowerCase()) {
    case 'completed':
      return 'success';
    case 'running':
      return 'info';
    case 'failed':
      return 'error';
    case 'cancelled':
      return 'warning';
    default:
      return 'grey';
  }
}

function getStatusIcon(status) {
  switch (status?.toLowerCase()) {
    case 'completed':
      return 'mdi-check-circle';
    case 'running':
      return 'mdi-progress-clock';
    case 'failed':
      return 'mdi-alert-circle';
    case 'cancelled':
      return 'mdi-cancel';
    default:
      return 'mdi-information';
  }
}

function formatDate(dateString) {
  if (!dateString) return '-';
  const date = new Date(dateString);
  return date.toLocaleString();
}

function formatDuration(milliseconds) {
  if (!milliseconds) return '-';
  
  const seconds = Math.floor(milliseconds / 1000);
  const minutes = Math.floor(seconds / 60);
  const hours = Math.floor(minutes / 60);
  
  if (hours > 0) {
    return `${hours}h ${minutes % 60}m`;
  } else if (minutes > 0) {
    return `${minutes}m ${seconds % 60}s`;
  } else {
    return `${seconds}s`;
  }
}

function getErrorRateColor(rate) {
  if (!rate) return 'success';
  if (rate < 0.01) return 'success';
  if (rate < 0.05) return 'warning';
  return 'error';
}

function formatErrorRate(rate) {
  if (!rate) return '0%';
  return `${(rate * 100).toFixed(2)}%`;
}

function getLogLevelColor(level) {
  if (!level) return 'primary';
  switch (level.toLowerCase()) {
    case 'error':
      return 'error';
    case 'warn':
    case 'warning':
      return 'warning';
    case 'info':
      return 'info';
    case 'debug':
      return 'grey';
    default:
      return 'primary';
  }
}

function formatLogDate(timestamp) {
  if (!timestamp) return '';
  const date = new Date(timestamp);
  return date.toLocaleString();
}

// Single-line formatter for messages + details
function formatLogSingleLine(log) {
  const msg = (log.message || '').toString().replace(/\s+/g, ' ').replace(/\n/g, ' ↵ ').trim();
  const details = log.details ? (' — ' + log.details.toString().replace(/\s+/g, ' ').replace(/\n/g, ' ↵ ').trim()) : '';
  return `${msg}${details}`;
}  
</script>

<style scoped>
.execution-log {
  font-family: monospace;
  white-space: pre-wrap;
  color: #e0e0e0;
  font-size: 0.875rem;
  line-height: 1.4;
}

.execution-logs {
  max-height: 300px;
  overflow-y: auto;
}

.log-entry {
  padding: 8px 0;
  border-bottom: 1px solid rgba(255, 255, 255, 0.1);
  font-family: 'JetBrains Mono', 'Fira Code', 'Consolas', monospace;
  font-size: 0.875rem;
  line-height: 1.4;
}

.log-entry:last-child {
  border-bottom: none;
}

.log-entry.log-error {
  background-color: rgba(244, 67, 54, 0.1);
}

.log-entry.log-warn,
.log-entry.log-warning {
  background-color: rgba(255, 152, 0, 0.1);
}

.log-entry.log-info {
  background-color: rgba(33, 150, 243, 0.1);
}

.log-row {
  display: flex;
  align-items: center;
  gap: 12px;
}

.log-left {
  flex: 0 0 170px;
}

.log-right {
  flex: 0 0 auto;
  display: flex;
  align-items: center;
  gap: 8px;
}

.log-timestamp {
  color: #888;
  font-size: 0.75rem;
}

.log-level {
  padding: 2px 6px;
  border-radius: 3px;
  font-size: 0.7rem;
  font-weight: bold;
  text-transform: uppercase;
}

.log-message {
  color: #e0e0e0;
  flex: 1 1 auto;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

/* details merged into the message; hidden */
.log-details { display: none; }

.log-level.level-error {
  background-color: #f44336;
  color: white;
}

.log-level.level-warn,
.log-level.level-warning {
  background-color: #ff9800;
  color: white;
}

.log-level.level-info {
  background-color: #2196f3;
  color: white;
}

.log-level.level-debug {
  background-color: #9e9e9e;
  color: white;
}



.log-message {
  color: #e0e0e0;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

/* details merged into message; keep hidden */
.log-details { display: none; }
</style>