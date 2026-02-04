<template>
  <div>
    <div class="d-flex align-center mb-4">
      <h1 class="text-h4 mr-4">{{ $t('executions.title') }}</h1>
      <v-spacer />
      <v-btn 
        color="primary" 
        :loading="loading"
        @click="fetchExecutions"
      >
        <v-icon v-if="$vuetify.display.smAndUp" class="mr-2">mdi-refresh</v-icon>
        <span>{{ $t('common.refresh') }}</span>
      </v-btn>
    </div>

    <v-card>
      <v-card-text>
        <v-row>
          <v-col cols="12" md="4">
            <v-text-field
              v-model="search"
              :label="$t('executions.searchExecutions')"
              prepend-inner-icon="mdi-magnify"
              density="compact"
              hide-details
              class="mb-4"
              @update:model-value="fetchExecutions"
            />
          </v-col>
          <v-col cols="12" md="3">
            <v-select
              v-model="statusFilter"
              :label="$t('common.status')"
              :items="statusOptions"
              density="compact"
              hide-details
              class="mb-4"
              @update:model-value="fetchExecutions"
            />
          </v-col>
          <v-col cols="12" md="3">
            <v-select
              v-model="timeRangeFilter"
              :label="$t('executions.timeRange')"
              :items="timeRangeOptions"
              density="compact"
              hide-details
              class="mb-4"
              @update:model-value="fetchExecutions"
            />
          </v-col>
        </v-row>

        <v-data-table
          :headers="headers"
          :items="executions"
          :loading="loading"
          :items-per-page="10"
          class="mt-2"
        >
          <template #item.status="{ item }">
            <v-chip
              :color="getStatusColor(item.status)"
              text-color="white"
              size="small"
            >
              {{ getStatusLabel(item.status) }}
            </v-chip>
          </template>
          <template #item.startTime="{ item }">
            {{ formatDate(item.startTime) }}
          </template>
          <template #item.endTime="{ item }">
            {{ item.endTime ? formatDate(item.endTime) : '-' }}
          </template>
          <template #item.durationMs="{ item }">
            {{ formatDuration(item.durationMs) }}
          </template>
          <template #item.actions="{ item }">
            <v-btn
              icon
              variant="text"
              size="small"
              title="View details"
              @click="viewExecutionDetails(item)"
            >
              <v-icon>mdi-eye</v-icon>
            </v-btn>
            <v-btn
              v-if="item.status === 'Running'"
              icon
              variant="text"
              size="small"
              color="error"
              title="Cancel execution"
              @click="cancelExecution(item)"
            >
              <v-icon>mdi-stop</v-icon>
            </v-btn>
          </template>
        </v-data-table>
      </v-card-text>
    </v-card>

    <!-- Execution Details Dialog -->
    <v-dialog
      v-model="showDetailsDialog"
      max-width="950px"
    >
      <v-card v-if="selectedExecution">
        <!-- Clean professional header -->
        <v-card-title class="d-flex align-center flex-shrink-0">
          <v-icon :icon="getStatusIcon(selectedExecution.status)" :color="getStatusColor(selectedExecution.status)" class="mr-2" size="large"></v-icon>
          <div class="flex-grow-1">
            <div class="text-h6">{{ selectedExecution.pipelineName }}</div>
          </div>
          <v-btn
            icon
            variant="text"
            @click="showDetailsDialog = false"
          >
            <v-icon>mdi-close</v-icon>
          </v-btn>
        </v-card-title>

        <v-divider></v-divider>

        <!-- Content with tabs -->
        <v-tabs v-model="activeTab" class="execution-tabs flex-shrink-0" grow>
          <v-tab value="overview">{{ $t('executions.overview') }}</v-tab>
          <v-tab value="logs">{{ $t('executions.logs') }}</v-tab>
          <v-tab value="timeline">{{ $t('executions.timeline') }}</v-tab>
        </v-tabs>

        <v-divider></v-divider>

        <v-card-text class="pa-0" style="max-height: 60vh; overflow-y: auto;">
          <v-window v-model="activeTab">
            <!-- Overview Tab - KEEP AS IS -->
            <v-window-item value="overview">
              <v-card-text>
                <v-row>
                  <v-col cols="12" lg="6">
                    <v-card variant="outlined" class="mb-4">
                      <v-card-title class="text-subtitle-1 d-flex align-center">
                        <v-icon class="mr-2">mdi-information-outline</v-icon>
                        <span>{{ $t('executions.basicInformation') }}</span>
                      </v-card-title>
                      <v-card-text>
                        <v-list density="compact" lines="two">
                          <v-list-item>
                            <template #prepend>
                              <v-icon class="mr-2">mdi-pipe</v-icon>
                            </template>
                            <v-list-item-title>{{ $t('dashboard.pipeline') }}</v-list-item-title>
                            <v-list-item-subtitle>{{ selectedExecution.pipelineName }}</v-list-item-subtitle>
                          </v-list-item>
                        
                          <v-divider></v-divider>
                        
                          <v-list-item>
                            <template #prepend>
                              <v-icon class="mr-2">mdi-calendar-clock</v-icon>
                            </template>
                            <v-list-item-title>{{ $t('dashboard.startTime') }}</v-list-item-title>
                            <v-list-item-subtitle>{{ formatDate(selectedExecution.startTime, true) }}</v-list-item-subtitle>
                          </v-list-item>
                        
                          <v-divider></v-divider>
                        
                          <v-list-item>
                            <template #prepend>
                              <v-icon class="mr-2">mdi-calendar-check</v-icon>
                            </template>
                            <v-list-item-title>{{ $t('executions.endTime') }}</v-list-item-title>
                            <v-list-item-subtitle>{{ selectedExecution.endTime ? formatDate(selectedExecution.endTime, true) : $t('executions.running') }}</v-list-item-subtitle>
                          </v-list-item>
                        
                          <v-divider></v-divider>
                        
                          <v-list-item>
                            <template #prepend>
                              <v-icon class="mr-2">mdi-timer-outline</v-icon>
                            </template>
                            <v-list-item-title>{{ $t('dashboard.duration') }}</v-list-item-title>
                            <v-list-item-subtitle>{{ formatDuration(selectedExecution.durationMs) }}</v-list-item-subtitle>
                          </v-list-item>
                        
                          <v-divider></v-divider>
                        
                          <v-list-item>
                            <template #prepend>
                              <v-icon class="mr-2">mdi-identifier</v-icon>
                            </template>
                            <v-list-item-title>{{ $t('executions.executionId') }}</v-list-item-title>
                            <v-list-item-subtitle>{{ selectedExecution.id }}</v-list-item-subtitle>
                          </v-list-item>
                        </v-list>
                      </v-card-text>
                    </v-card>
                  </v-col>
                
                  <v-col cols="12" lg="6">
                    <v-card variant="outlined" class="mb-4">
                      <v-card-title class="text-subtitle-1 d-flex align-center">
                        <v-icon class="mr-2">mdi-chart-bar</v-icon>
                        <span>{{ $t('executions.performanceMetrics') }}</span>
                      </v-card-title>
                      <v-card-text>
                        <div class="my-3">
                          <div class="d-flex justify-space-between mb-1">
                            <span class="text-body-2 font-weight-medium">{{ $t('executions.progress') }}</span>
                            <span class="text-body-2">{{ Math.round(selectedExecution.progressPercent || 0) }}%</span>
                          </div>
                          <v-progress-linear
                            :model-value="selectedExecution.status === 'Running' ? (selectedExecution.progressPercent || 0) : 100"
                            :color="getStatusColor(selectedExecution.status)"
                            height="10"
                            rounded
                            :striped="selectedExecution.status === 'Running'"
                          ></v-progress-linear>
                        </div>
                      
                        <v-divider class="my-4"></v-divider>
                      
                        <div class="d-flex align-center mb-2">
                          <v-icon class="mr-2">mdi-database</v-icon>
                          <span class="text-subtitle-2 font-weight-bold">{{ $t('dashboard.rowsProcessed') }}</span>
                        </div>
                        <div class="text-h4 ml-8">
                          {{ selectedExecution.rowsProcessed?.toLocaleString() || '0' }}
                        </div>
                      
                        <v-divider class="my-4"></v-divider>
                      
                        <div class="d-flex align-center mb-2">
                          <v-icon class="mr-2">mdi-information-outline</v-icon>
                          <div class="d-flex align-center" style="gap: 12px;">
                            <span class="text-subtitle-2 font-weight-bold">{{ $t('common.status') }}</span>
                            <v-chip
                              :color="getStatusColor(selectedExecution.status)"
                              text-color="white"
                            >
                              {{ getStatusLabel(selectedExecution.status) }}
                            </v-chip>
                            <v-btn
                              v-if="selectedExecution.status === 'Running'"
                              color="error"
                              variant="tonal"
                              size="small"
                              @click="cancelExecution(selectedExecution)"
                            >
                              <v-icon start size="small">mdi-stop</v-icon>
                              {{ $t('common.cancel') }}
                            </v-btn>
                          </div>
                        </div>
                      </v-card-text>
                    </v-card>
                  </v-col>
                </v-row>
              </v-card-text>
            </v-window-item>
          
            <!-- Logs Tab -->
            <v-window-item value="logs">
              <v-card-text>
                <div class="d-flex align-center mb-3">
                  <v-icon class="mr-2">mdi-text-box-outline</v-icon>
                  <div class="text-subtitle-1 font-weight-bold">{{ $t('executions.executionLogs') }}</div>
                  <v-spacer></v-spacer>
                  <v-btn
                    density="compact"
                    variant="text"
                    size="small"
                    prepend-icon="mdi-content-copy"
                    @click="copyLogs"
                  >
                    {{ $t('common.copy') }}
                  </v-btn>
                </div>
              
                <v-card
                  variant="outlined"
                  class="logs-container custom-scrollbar"
                >
                  <div v-if="!selectedExecution.logs || selectedExecution.logs.length === 0" class="pa-4 text-center">
                    <v-icon size="48" color="grey-lighten-1" class="mb-2">mdi-text-box-outline</v-icon>
                    <div class="text-body-1 text-grey">{{ $t('executions.noLogsAvailable') }}</div>
                  </div>
                  <div v-else class="logs-entries">
                    <div
                      v-for="(log, index) in selectedExecution.logs"
                      :key="index"
                      class="log-entry"
                      :class="`log-${log.level?.toLowerCase()}`"
                    >
                      <div class="log-row">
                        <div class="log-left">
                          <span class="log-timestamp">{{ formatDate(log.timestamp, true) }}</span>
                        </div>
                        <div class="log-message">{{ formatLogSingleLine(log) }}</div>
                        <div class="log-right">
                          <v-chip size="small" :color="getLogLevelColor(log.level)" text-color="white" class="log-level-chip">
                            {{ log.level }}
                          </v-chip>
                        </div>
                      </div>
                    </div>
                  </div>
                </v-card>
              </v-card-text>
            </v-window-item>
          
            <!-- Timeline Tab - IMPROVED WITH SCROLLING -->
            <v-window-item value="timeline">
              <v-card-text>
                <div class="d-flex align-center mb-3">
                  <v-icon class="mr-2">mdi-timeline</v-icon>
                  <div class="text-subtitle-1 font-weight-bold">{{ $t('executions.executionTimeline') }}</div>
                </div>
              
                <div v-if="!getExecutionSteps().length" class="text-center pa-4">
                  <v-icon size="48" color="grey-lighten-1" class="mb-2">mdi-timeline-clock</v-icon>
                  <div class="text-body-1 text-grey">{{ $t('executions.noTimelineEvents') }}</div>
                </div>
              
                <!-- Added scrollable container -->
                <div v-else class="timeline-scrollable-container custom-scrollbar">
                  <v-timeline side="end" line-color="grey-lighten-1" class="timeline-container">
                    <v-timeline-item
                      v-for="(step, index) in getExecutionSteps()"
                      :key="index"
                      :dot-color="step.color"
                      :icon="step.icon"
                      class="app-timeline-item"
                      :class="`timeline-${step.level}`"
                    >
                      <template #opposite>
                        <div class="text-caption text-grey timeline-time">{{ formatTimelineTime(step.time) }}</div>
                      </template>
                    
                      <v-card variant="outlined" class="app-timeline-card" :class="`card-${step.level}`" density="compact">
                        <v-card-text class="pa-2 d-flex align-center" style="gap: 12px;">
                          <v-icon size="18" :color="step.color">{{ step.icon }}</v-icon>
                          <div class="timeline-text">
                            <div class="timeline-title">{{ step.title }}</div>
                            <div v-if="step.description" class="timeline-desc">{{ step.description }}</div>
                          </div>
                        </v-card-text>
                      </v-card>
                    </v-timeline-item>
                  </v-timeline> 
                </div>
              </v-card-text>
            </v-window-item>
          </v-window>
        </v-card-text>

        <v-divider></v-divider>

        <v-card-actions class="flex-shrink-0">
          <v-spacer />
          <v-btn
            color="primary"
            @click="showDetailsDialog = false"
          >
            {{ $t('common.close') }}
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <!-- Cancel Confirmation Dialog -->
    <v-dialog
      v-model="showCancelDialog"
      max-width="400px"
    >
      <v-card>
        <v-card-title class="text-h5">
          Cancel Execution
        </v-card-title>
        <v-card-text>
          Are you sure you want to cancel this pipeline execution? This will stop all ongoing processing.
        </v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn
            variant="text"
            @click="showCancelDialog = false"
          >
            No
          </v-btn>
          <v-btn
            color="error"
            :loading="cancelling"
            @click="confirmCancelExecution"
          >
            Yes, Cancel
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>
  </div>
</template>

<script setup>
import { ref, computed, onMounted, onBeforeUnmount, nextTick } from 'vue';
import { useI18n } from 'vue-i18n';
import { useTenantStore } from '@/stores/tenant';
import { useTheme } from 'vuetify';
import { getExecutions, getExecutionById } from '@/services/pipelineService';
import { useGlobalState } from '@/composables/useGlobalState';

const { t } = useI18n();
const { showSuccess, showError, showInfo } = useGlobalState();

const tenantStore = useTenantStore();
const theme = useTheme();

// Data table
const headers = computed(() => [
  { title: t('dashboard.pipeline'), key: 'pipelineName' },
  { title: t('common.status'), key: 'status', width: '120px' },
  { title: t('dashboard.startTime'), key: 'startTime', width: '150px' },
  { title: t('executions.endTime'), key: 'endTime', width: '150px' },
  { title: t('dashboard.duration'), key: 'durationMs', width: '100px' },
  { title: t('dashboard.rowsProcessed'), key: 'rowsProcessed', width: '120px' },
  { title: t('common.actions'), key: 'actions', sortable: false, width: '100px', align: 'end' }
]);

// Filters and sorting
const search = ref('');
const statusFilter = ref('All');
const timeRangeFilter = ref('24h');
const statusOptions = computed(() => [
  { title: t('filters.allStatuses'), value: 'All' },
  { title: t('executions.running'), value: 'Running' },
  { title: t('executions.completed'), value: 'Completed' },
  { title: t('executions.failed'), value: 'Failed' },
  { title: t('executions.cancelled'), value: 'Cancelled' }
]);
const timeRangeOptions = computed(() => [
  { title: t('executions.last24Hours'), value: '24h' },
  { title: t('executions.last7Days'), value: '7d' },
  { title: t('executions.last30Days'), value: '30d' },
  { title: t('executions.allTime'), value: 'all' }
]);

function getStatusLabel(status) {
  const statusMap = {
    'Running': t('executions.running'),
    'Completed': t('executions.completed'),
    'Failed': t('executions.failed'),
    'Cancelled': t('executions.cancelled'),
    'Queued': t('executions.queued')
  };
  return statusMap[status] || status;
}

// Execution data
const executions = ref([]);
const loading = ref(false);
const cancelling = ref(false);

// Dialog controls
const showDetailsDialog = ref(false);
const showCancelDialog = ref(false);
const selectedExecution = ref(null);
const executionToCancel = ref(null);

// Add to existing variables
const activeTab = ref('overview');

// Add these new functions
function getStatusIcon(status) {
  switch (status?.toLowerCase()) {
    case 'completed':
      return 'mdi-check-circle';
    case 'running':
      return 'mdi-circle-slice-8';
    case 'failed':
      return 'mdi-alert-circle';
    case 'cancelled':
      return 'mdi-stop-circle';
    case 'queued':
      return 'mdi-clock-outline';
    default:
      return 'mdi-help-circle';
  }
}

function getLogLevelColor(level) {
  switch (level?.toLowerCase()) {
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

function copyLogs() {
  if (selectedExecution.value?.logs && selectedExecution.value.logs.length > 0) {
    const logText = selectedExecution.value.logs
      .map(log => `[${formatDate(log.timestamp, true)}] ${log.level}: ${formatLogSingleLine(log)}`)
      .join('\n');
    
    navigator.clipboard.writeText(logText)
      .then(() => {
        showInfo(t('executions.logsCopied'), t('executions.title'));
      })
      .catch(err => {
        console.error('Failed to copy logs: ', err);
        showError(t('executions.errors.copyLogsFailed'), t('common.error'));
      });
  }
}

// Single-line formatter for message + details
function formatLogSingleLine(log) {
  const msg = (log.message || '').toString().replace(/\s+/g, ' ').replace(/\n/g, ' ↵ ').trim();
  const details = log.details ? (' — ' + log.details.toString().replace(/\s+/g, ' ').replace(/\n/g, ' ↵ ').trim()) : '';
  return `${msg}${details}`;
}

// Format time for timeline display
function formatTimelineTime(timeString) {
  try {
    const date = new Date(timeString);
    return date.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit', second: '2-digit' });
  } catch (e) {
    return timeString;
  }
}

// Improve the getExecutionSteps function for better timeline display
function getExecutionSteps() {
  if (!selectedExecution.value || !selectedExecution.value.logs || selectedExecution.value.logs.length === 0) return [];
  
  return selectedExecution.value.logs.map((log, index) => {
    // Determine step type, icon and level based on log level and message content
    let color = 'primary';
    let icon = 'mdi-information';
    let level = (log.level || '').toLowerCase() || 'info';
    
    const message = (log.message || '').toLowerCase();

      // If message mentions failed counts, only mark error when failed > 0
    const failedMatch = message.match(/(\d+)\s+failed/);
    // Prefer source-based mapping for specific components like FieldMapping/DataReader/DataWriter
    const source = (log.source || '').toLowerCase();
    if (source.includes('field') || source.includes('fieldmapping')) {
      color = 'purple';
      icon = 'mdi-autorenew';
      level = 'info';
    } else if (source.includes('datareader')) {
      color = 'teal';
      icon = 'mdi-database-export';
      level = 'info';
    } else if (source.includes('datawriter')) {
      color = 'indigo';
      icon = 'mdi-database-import';
      level = 'info';
    } else if (failedMatch && parseInt(failedMatch[1], 10) > 0) {
      color = 'error';
      icon = 'mdi-alert-circle';
      level = 'error';
    } else if (level === 'error' || message.includes('error')) {
      color = 'error';
      icon = 'mdi-alert-circle';
      level = 'error';
    } else if (level === 'warn' || level === 'warning') {
      color = 'warning';
      icon = 'mdi-alert';
      level = 'warning';
    } else if (message.includes('completed') || message.includes('success') || message.includes('succeeded')) {
      color = 'success';
      icon = 'mdi-check-circle';
      level = 'success';
    } else if (message.includes('starting') || message.includes('started')) {
      color = 'info';
      icon = 'mdi-play-circle';
      level = 'info';
    } else if (message.includes('read') || message.includes('extracted')) {
      color = 'teal';
      icon = 'mdi-database-export';
      level = 'info';
    } else if (message.includes('writing') || message.includes('write')) {
      color = 'indigo';
      icon = 'mdi-database-import';
      level = 'info';
    } else if (message.includes('batch')) {
      color = 'primary';
      icon = 'mdi-buffer';
      level = level || 'info';
    }
    
    return {
      time: log.timestamp,
      title: `${log.source}: ${log.message}`,
      description: log.details,
      color,
      icon,
      level
    };
  });
}

function scrollToFirstError() {
  try {
    const el = document.querySelector('.app-timeline-item.timeline-error');
    if (el) {
      el.scrollIntoView({ behavior: 'smooth', block: 'center' });
    }
  } catch (e) {
    // ignore
  }
} 

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
    case 'queued':
      return 'blue-grey';
    default:
      return 'grey';
  }
}

function formatDate(dateString, includeSeconds = false) {
  if (!dateString) return '-';
  const date = new Date(dateString);
  
  if (includeSeconds) {
    return date.toLocaleString();
  } else {
    return date.toLocaleDateString() + ' ' + date.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
  }
}

function formatDuration(milliseconds) {
  if (!milliseconds && milliseconds !== 0) {
    return '-';
  }
  
  const seconds = Math.floor(milliseconds / 1000);
  const minutes = Math.floor(seconds / 60);
  const hours = Math.floor(minutes / 60);
  
  if (hours > 0) {
    return `${hours}h ${minutes % 60}m`;
  } else if (minutes > 0) {
    return `${minutes}m ${seconds % 60}s`;
  } else if (seconds > 0) {
    return `${seconds}s`;
  } else {
    // Show milliseconds for sub-second durations
    return `${milliseconds}ms`;
  }
}

function getTimeRangeFilter() {
  const now = new Date();
  
  switch (timeRangeFilter.value) {
    case '24h':
      return new Date(now.getTime() - 24 * 60 * 60 * 1000);
    case '7d':
      return new Date(now.getTime() - 7 * 24 * 60 * 60 * 1000);
    case '30d':
      return new Date(now.getTime() - 30 * 24 * 60 * 60 * 1000);
    case 'all':
    default:
      return null;
  }
}

async function fetchExecutions() {
  try {
    loading.value = true;
    
    // Call real API
    const filters = {
      search: search.value || undefined,
      status: statusFilter.value !== 'All' ? statusFilter.value : undefined,
      page: 1,
      pageSize: 100 // Get more records for client-side filtering
    };
    
    const apiExecutions = await getExecutions(filters);
    
    // Transform API response to match view format
    executions.value = apiExecutions.map(exec => {
      // Format logs from array to string
      let logsText = '';
      if (exec.logs && exec.logs.length > 0) {
        logsText = exec.logs.map(log => 
          `[${new Date(log.timestamp).toLocaleString()}] ${log.level}: ${log.message}`
        ).join('\n');
      } else {
        logsText = `[${new Date(exec.startTime).toLocaleString()}] Pipeline execution ${exec.status.toLowerCase()}`;
      }
      
      return {
        id: exec.id,
        pipelineName: exec.pipelineName,
        pipelineId: exec.pipelineId,
        status: exec.status,
        startTime: exec.startTime,
        endTime: exec.endTime,
        durationMs: exec.durationMs,
        rowsProcessed: exec.recordsProcessed || 0,
        progressPercent: exec.progressPercent || 0,
        logs: logsText,
        errors: []
      };
    });
  } catch (error) {
    console.error('Error fetching executions:', error);
    executions.value = [];
    showError(t('executions.errors.loadFailed'), t('common.error'));
  } finally {
    loading.value = false;
  }
}

onMounted(async () => {
  await fetchExecutions();
});

// Clean up the component setup
const handleRefresh = () => {
  fetchExecutions();
};


async function viewExecutionDetails(execution) {
  try {
    // Fetch detailed execution data including logs
    const detailedExecution = await getExecutionById(execution.id);
    selectedExecution.value = detailedExecution;
    showDetailsDialog.value = true;

    // Wait for DOM update then scroll to first error (if any)
    await nextTick();
    scrollToFirstError();
  } catch (error) {
    console.error('Failed to fetch execution details:', error);
    showError(t('executions.errors.fetchDetailsFailed'), t('common.error'));
  }
} 

function cancelExecution(execution) {
  executionToCancel.value = execution;
  showCancelDialog.value = true;
}

async function confirmCancelExecution() {
  try {
    cancelling.value = true;
    
    // Call the real API to cancel execution
    const { cancelExecution: cancelExecutionApi } = await import('@/services/pipelineService');
    const updatedExecution = await cancelExecutionApi(executionToCancel.value.id);
    
    // Update the status in the local array
    const execution = executions.value.find(e => e.id === executionToCancel.value.id);
    if (execution) {
      execution.status = updatedExecution.status;
      execution.endTime = updatedExecution.endTime;
      execution.duration = updatedExecution.duration;
      
      // If this execution is currently shown in details dialog, update it
      if (selectedExecution.value && selectedExecution.value.id === execution.id) {
        selectedExecution.value = { ...execution };
      }
    }
    
    showCancelDialog.value = false;
    executionToCancel.value = null;
    showSuccess(t('executions.cancelSuccess'), t('executions.title'));
  } catch (error) {
    console.error('Error cancelling execution:', error);
    showError(t('executions.errors.cancelFailed'), t('common.error'));
  } finally {
    cancelling.value = false;
  }
}

</script>

<style scoped>
.log-container {
  max-height: 450px;
  overflow-y: auto;
  border-radius: 8px;
}

.logs {
  font-family: 'Consolas', 'Monaco', monospace;
  white-space: pre-wrap;
  font-size: 13px;
}

.log-text {
  color: rgb(var(--v-theme-on-surface));
}

/* Tab styling */
.execution-tabs {
  border-bottom: 1px solid rgba(var(--v-theme-on-surface), 0.12);
}

:deep(.v-tab) {
  min-height: 48px;
  opacity: 0.7;
}

:deep(.v-tab--selected) {
  opacity: 1;
}

:deep(.v-theme--light .v-tab--selected) {
  color: var(--v-theme-primary);
}

/* Mobile responsive improvements */
@media (max-width: 600px) {
  :deep(.v-dialog .v-card) {
    max-height: 90vh !important;
    display: flex;
    flex-direction: column;
  }
  
  :deep(.v-card-title) {
    font-size: 0.875rem !important;
    padding: 12px !important;
  }
  
  :deep(.v-list-item-title) {
    font-size: 0.75rem !important;
  }
  
  :deep(.v-list-item-subtitle) {
    font-size: 0.75rem !important;
  }
  
  :deep(.v-tab) {
    font-size: 0.75rem !important;
    min-width: 80px !important;
  }
}

/* Timeline specific styles */
.timeline-container {
  padding: 12px 0;
}

.timeline-item {
  margin-bottom: 0 !important;
}

:deep(.v-timeline-item__body) {
  margin-bottom: 16px !important;
  max-width: 85%; /* Limit width of timeline items */
}

:deep(.v-timeline-item__dot) {
  margin-left: 0 !important;
  margin-right: 0 !important;
}

:deep(.v-timeline-item__dot--filled .v-timeline-item__inner-dot) {
  box-shadow: 0 2px 4px rgba(var(--v-theme-on-surface), 0.1);
}

:deep(.v-timeline-divider__line) {
  border-left-width: 2px !important;
}

.timeline-title {
  word-break: break-word; /* Prevent long titles from overflowing */
  line-height: 1.4;
}

.timeline-description {
  margin-bottom: 0;
  word-break: break-word;
}

.timeline-time {
  font-family: monospace;
  font-size: 11px;
  margin-right: 12px;
  white-space: nowrap;
}

/* Ensure timeline divider line extends properly */
:deep(.v-timeline-divider) {
  min-height: 100%;
}

/* Fix alignment of timeline dots to cards */
:deep(.v-timeline-item__opposite) {
  display: flex;
  align-items: center;
  justify-content: flex-end;
}

/* Improve positioning of timeline events */
:deep(.v-timeline) {
  align-items: flex-start;
}

/* Added scrollable container styles */
.timeline-scrollable-container {
  max-height: 400px;
  overflow-y: auto;
  border-radius: var(--app-border-radius);
}

/* Mobile responsive adjustments */
@media (max-width: 600px) {
  .timeline-scrollable-container {
    max-height: 300px;
  }
  
  :deep(.v-timeline-item__opposite) {
    display: none !important;
  }
}

/* Theme-specific styling */
.v-theme--dark :deep(.v-timeline-divider__line) {
  border-left-color: rgba(255, 255, 255, 0.12) !important;
}

.v-theme--light :deep(.v-timeline-divider__line) {
  border-left-color: rgba(0, 0, 0, 0.12) !important;
}

/* Updated logs styling for better light/dark mode support */
.logs-container {
  max-height: 450px;
  overflow-y: auto;
  border-radius: var(--app-border-radius);
  font-family: 'Consolas', 'Monaco', monospace;
  white-space: pre-wrap;
  font-size: 13px;
  padding: 0;
  border: 1px solid;
}

.logs-content {
  padding: 16px;
  margin: 0;
  line-height: 1.5;
}

/* Logs styling */
.v-theme--dark :deep(.logs-container) {
  background-color: rgb(var(--v-theme-surface-variant));
  border-color: rgba(255, 255, 255, 0.12);
}

.v-theme--light :deep(.logs-container) {
  background-color: rgb(var(--v-theme-surface-variant));
  border-color: rgba(0, 0, 0, 0.12);
}

:deep(.logs-content) {
  color: rgb(var(--v-theme-on-surface));
}

/* Timeline cards */
:deep(.app-timeline-card) {
  border-left: 3px solid rgba(0,0,0,0.1);
  transition: all 0.18s ease;
  background: rgba(var(--v-theme-surface), 0.02);
}

:deep(.app-timeline-card:hover) {
  border-left-color: rgba(var(--v-theme-secondary), 0.7);
  box-shadow: 0 6px 18px rgba(var(--v-theme-on-surface), 0.08);
}

:deep(.app-timeline-card .timeline-title) {
  font-weight: 600;
}

:deep(.app-timeline-card .timeline-desc) {
  font-size: 0.9rem;
  color: rgb(var(--v-theme-on-surface-variant));
}

:deep(.card-error) { border-left-color: rgb(var(--v-theme-error)); }
:deep(.card-warning) { border-left-color: rgb(var(--v-theme-warning)); }
:deep(.card-success) { border-left-color: rgb(var(--v-theme-success)); }
:deep(.card-info) { border-left-color: rgb(var(--v-theme-info)); }

/* Make timeline container scrollable */
.timeline-scrollable-container { max-height: 350px; overflow-y: auto; padding-right: 8px; }


/* Make execution dialog consistent between themes */
:deep(.v-dialog > .v-card) {
  border-radius: var(--app-border-radius);
  overflow: hidden;
}

/* Log entries styling */
.logs-entries {
  max-height: 400px;
  overflow-y: auto;
}

.log-entry {
  padding: 12px;
  border-bottom: 1px solid rgb(var(--v-theme-surface-variant));
  font-family: 'JetBrains Mono', 'Fira Code', 'Consolas', monospace;
  font-size: 0.875rem;
  line-height: 1.4;
}

.log-entry:last-child {
  border-bottom: none;
}

.log-entry.log-error {
  background-color: rgba(var(--v-theme-error), 0.05);
  border-left: 3px solid rgb(var(--v-theme-error));
}

.log-entry.log-warn,
.log-entry.log-warning {
  background-color: rgba(var(--v-theme-warning), 0.05);
  border-left: 3px solid rgb(var(--v-theme-warning));
}

.log-entry.log-info {
  background-color: rgba(var(--v-theme-info), 0.05);
  border-left: 3px solid rgb(var(--v-theme-info));
}

.log-row {
  display: flex;
  align-items: center;
  gap: 12px;
}

.log-left {
  flex: 0 0 170px; /* fixed-ish column for timestamp */
}

.log-right {
  flex: 0 0 auto;
  display: flex;
  align-items: center;
  gap: 8px;
}

.log-timestamp {
  color: rgb(var(--v-theme-on-surface-variant));
  font-size: 0.75rem;
  font-weight: 500;
}

.log-level-chip {
  font-size: 0.72rem;
  height: 22px;
  min-width: 56px;
}

.log-source {
  color: rgb(var(--v-theme-primary));
  font-weight: 600;
  font-size: 0.8rem;
}

.log-message {
  color: rgb(var(--v-theme-on-surface));
  flex: 1 1 auto;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

/* details are merged into the single-line representation */
.log-details { display: none; } 

</style>
