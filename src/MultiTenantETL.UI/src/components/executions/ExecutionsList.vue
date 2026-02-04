<template>
  <v-card elevation="2">
    <v-card-title class="d-flex align-center">
      <v-icon icon="mdi-history" class="mr-2" />
      {{ title }}
      <v-spacer />
      <slot name="actions" />
    </v-card-title>
    <v-card-text>
      <v-table>
        <thead>
          <tr>
            <th>Pipeline</th>
            <th>Start Time</th>
            <th>Duration</th>
            <th>Status</th>
            <th>Rows Processed</th>
          </tr>
        </thead>
        <tbody>
          <tr v-if="loading">
            <td colspan="5" class="text-center">
              <v-progress-circular indeterminate class="ma-4" />
            </td>
          </tr>
          <tr v-else-if="!executions.length">
            <td colspan="5" class="text-center">No executions found</td>
          </tr>
          <tr v-for="execution in executions" :key="execution.id" class="execution-row" @click="$emit('select', execution)">
            <td>
              <div class="d-flex align-center">
                <v-icon :icon="getStatusIcon(execution.status)" class="mr-2" size="small" :color="getStatusColor(execution.status)" />
                {{ execution.pipelineName }}
              </div>
            </td>
            <td>{{ formatDate(execution.startTime) }}</td>
            <td>{{ formatDuration(execution.durationMs) }}</td>
            <td>
              <v-chip
                size="small"
                :color="getStatusColor(execution.status)"
                text-color="white"
                :prepend-icon="getStatusIcon(execution.status)"
              >
                {{ execution.status }}
              </v-chip>
            </td>
            <td>{{ execution.rowsProcessed?.toLocaleString() || '-' }}</td>
          </tr>
        </tbody>
      </v-table>
    </v-card-text>
  </v-card>
</template>

<script setup>
defineProps({
  title: {
    type: String,
    default: 'Pipeline Executions'
  },
  executions: {
    type: Array,
    required: true
  },
  loading: {
    type: Boolean,
    default: false
  }
});

defineEmits(['select']);

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
</script>

<style scoped>
.execution-row {
  cursor: pointer;
}

.execution-row:hover {
  background-color: rgba(var(--v-theme-primary), 0.05);
}
</style>