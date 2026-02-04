import { ref, computed } from 'vue';
import { fetchDashboardData, refreshExecutionStats, refreshRecentExecutions } from '@/services/dashboardService';
import { useTenantStore } from '@/stores/tenant';

/**
 * Composable for dashboard functionality
 * @returns {Object} Dashboard composable functions and reactive data
 */
export function useDashboard() {
  const tenantStore = useTenantStore();
  const loading = ref(true);
  const error = ref(null);
  const recentExecutions = ref([]);
  const stats = ref({
    totalPipelines: 0,
    activePipelines: 0,
    connectors: 0,
    schedules: 0,
    totalExecutions: 0,
    runningExecutions: 0,
    completedExecutions: 0,
    failedExecutions: 0,
    cancelledExecutions: 0,
    successRate: 0,
    averageDurationMs: null,
    totalRecordsProcessed: 0,
    lastExecutionTime: null
  });
  const statusDistribution = ref([
    { name: 'Completed', count: 0 },
    { name: 'Running', count: 0 },
    { name: 'Failed', count: 0 },
    { name: 'Cancelled', count: 0 }
  ]);

  // Computed properties for formatted values
  const formattedSuccessRate = computed(() => {
    const rate = stats.value.successRate;
    if (rate === null || rate === undefined) return '0%';
    return `${rate.toFixed(1)}%`;
  });

  const formattedTotalRecords = computed(() => {
    const records = stats.value.totalRecordsProcessed;
    if (!records) return '0';
    if (records >= 1000000) return `${(records / 1000000).toFixed(1)}M`;
    if (records >= 1000) return `${(records / 1000).toFixed(1)}K`;
    return records.toLocaleString();
  });

  const formattedAverageDuration = computed(() => {
    return formatDuration(stats.value.averageDurationMs);
  });

  const hasRunningExecutions = computed(() => {
    return stats.value.runningExecutions > 0;
  });

  /**
   * Get the appropriate color for a status
   * @param {string} status - The status name
   * @returns {string} Color name
   */
  function getStatusColor(status) {
    switch (status?.toLowerCase()) {
      case 'completed':
        return 'success';
      case 'running':
        return 'info';
      case 'queued':
        return 'blue-grey';
      case 'failed':
        return 'error';
      case 'cancelled':
        return 'warning';
      default:
        return 'grey';
    }
  }

  /**
   * Get the appropriate icon for a status
   * @param {string} status - The status name
   * @returns {string} Icon name
   */
  function getStatusIcon(status) {
    switch (status?.toLowerCase()) {
      case 'completed':
        return 'mdi-check-circle';
      case 'running':
        return 'mdi-progress-clock';
      case 'queued':
        return 'mdi-clock-outline';
      case 'failed':
        return 'mdi-alert-circle';
      case 'cancelled':
        return 'mdi-cancel';
      default:
        return 'mdi-information';
    }
  }

  /**
   * Format a date string
   * @param {string} dateString - ISO date string
   * @returns {string} Formatted date string
   */
  function formatDate(dateString) {
    if (!dateString) return '-';
    const date = new Date(dateString);
    return date.toLocaleString();
  }

  /**
   * Format relative time
   * @param {string} dateString - ISO date string
   * @returns {string} Relative time string
   */
  function formatRelativeTime(dateString) {
    if (!dateString) return '-';
    const date = new Date(dateString);
    const now = new Date();
    const diffMs = now - date;
    const diffMins = Math.floor(diffMs / 60000);
    const diffHours = Math.floor(diffMs / 3600000);
    const diffDays = Math.floor(diffMs / 86400000);

    if (diffMins < 1) return 'Just now';
    if (diffMins < 60) return `${diffMins}m ago`;
    if (diffHours < 24) return `${diffHours}h ago`;
    if (diffDays < 7) return `${diffDays}d ago`;
    return date.toLocaleDateString();
  }

  /**
   * Format a duration in milliseconds
   * @param {number} milliseconds - Duration in milliseconds
   * @returns {string} Formatted duration string
   */
  function formatDuration(milliseconds) {
    if (!milliseconds && milliseconds !== 0) return '-';

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
      return `${milliseconds}ms`;
    }
  }

  /**
   * Fetch dashboard data from the API
   */
  async function loadDashboardData() {
    try {
      loading.value = true;
      error.value = null;

      const data = await fetchDashboardData();

      stats.value = data.stats;
      statusDistribution.value = data.statusDistribution;
      recentExecutions.value = data.recentExecutions;

    } catch (err) {
      console.error('Error loading dashboard data:', err);
      error.value = err.message || 'Failed to load dashboard data';
    } finally {
      loading.value = false;
    }
  }

  /**
   * Refresh only execution stats (lighter refresh)
   */
  async function refreshStats() {
    try {
      const executionStats = await refreshExecutionStats();
      stats.value = {
        ...stats.value,
        totalExecutions: executionStats.totalExecutions,
        runningExecutions: executionStats.runningExecutions,
        completedExecutions: executionStats.completedExecutions,
        failedExecutions: executionStats.failedExecutions,
        cancelledExecutions: executionStats.cancelledExecutions,
        successRate: executionStats.successRate,
        averageDurationMs: executionStats.averageDurationMs,
        totalRecordsProcessed: executionStats.totalRecordsProcessed,
        lastExecutionTime: executionStats.lastExecutionTime
      };

      statusDistribution.value = [
        { name: 'Completed', count: executionStats.completedExecutions || 0 },
        { name: 'Running', count: executionStats.runningExecutions || 0 },
        { name: 'Failed', count: executionStats.failedExecutions || 0 },
        { name: 'Cancelled', count: executionStats.cancelledExecutions || 0 }
      ];
    } catch (err) {
      console.error('Error refreshing stats:', err);
    }
  }

  /**
   * Refresh only recent executions
   */
  async function refreshExecutions() {
    try {
      const executions = await refreshRecentExecutions(5);
      recentExecutions.value = executions.map(exec => ({
        id: exec.id,
        pipelineId: exec.pipelineId,
        pipelineName: exec.pipelineName,
        startTime: exec.startTime,
        endTime: exec.endTime,
        duration: exec.duration,
        durationMs: exec.durationMs,
        status: exec.status,
        rowsProcessed: exec.recordsProcessed || 0,
        progressPercent: exec.progressPercent || 0,
        triggeredBy: exec.triggeredBy
      }));
    } catch (err) {
      console.error('Error refreshing executions:', err);
    }
  }

  /**
   * Setup tenant subscription to refresh data when tenant changes
   */
  function setupTenantSubscription() {
    tenantStore.$subscribe(() => {
      if (tenantStore.currentTenantId) {
        loadDashboardData();
      }
    });
  }

  return {
    // Reactive data
    loading,
    error,
    stats,
    recentExecutions,
    statusDistribution,

    // Computed
    formattedSuccessRate,
    formattedTotalRecords,
    formattedAverageDuration,
    hasRunningExecutions,

    // Methods
    getStatusColor,
    getStatusIcon,
    formatDate,
    formatRelativeTime,
    formatDuration,
    loadDashboardData,
    refreshStats,
    refreshExecutions,
    setupTenantSubscription
  };
}