// Dashboard service for handling dashboard-related API calls
import api from './api'

/**
 * Fetches pipeline count
 * @returns {Promise<Object>} Paginated response with totalCount
 */
async function fetchPipelineStats() {
  try {
    const response = await api.get('/api/pipelines', { params: { page: 1, pageSize: 1 } })
    return {
      totalCount: response.data.totalCount || 0,
      pipelines: response.data.pipelines || []
    }
  } catch (error) {
    return { totalCount: 0, pipelines: [] }
  }
}

/**
 * Fetches active pipelines count
 * @returns {Promise<number>} Number of active pipelines
 */
async function fetchActivePipelinesCount() {
  try {
    const response = await api.get('/api/pipelines', { 
      params: { page: 1, pageSize: 1, isActive: true } 
    })
    return response.data.totalCount || 0
  } catch (error) {
    return 0
  }
}

/**
 * Fetches connector count
 * @returns {Promise<number>} Total number of connectors
 */
async function fetchConnectorCount() {
  try {
    const response = await api.post('/api/connectors/search', { 
      page: 1, 
      pageSize: 1 
    })
    return response.data.totalCount || 0
  } catch (error) {
    return 0
  }
}

/**
 * Fetches schedule count
 * @returns {Promise<number>} Total number of schedules
 */
async function fetchScheduleCount() {
  try {
    const response = await api.get('/api/schedules', { 
      params: { page: 1, pageSize: 1 } 
    })
    return response.data.totalCount || 0
  } catch (error) {
    return 0
  }
}

/**
 * Fetches execution statistics
 * @returns {Promise<Object>} Execution statistics
 */
async function fetchExecutionStats() {
  try {
    const response = await api.get('/api/executions/stats')
    return response.data
  } catch (error) {
    return {
      totalExecutions: 0,
      runningExecutions: 0,
      completedExecutions: 0,
      failedExecutions: 0,
      cancelledExecutions: 0,
      successRate: 0,
      averageDurationMs: null,
      totalRecordsProcessed: 0,
      lastExecutionTime: null
    }
  }
}

/**
 * Fetches recent executions
 * @param {number} limit - Number of recent executions to fetch
 * @returns {Promise<Array>} List of recent executions
 */
async function fetchRecentExecutions(limit = 5) {
  try {
    const response = await api.get('/api/executions', { 
      params: { 
        page: 1, 
        pageSize: limit,
        sortBy: 'start_time_desc'
      } 
    })
    return response.data.executions || []
  } catch (error) {
    return []
  }
}

/**
 * Fetches all dashboard data including stats and recent executions
 * @returns {Promise<Object>} Object containing stats and executions data
 */
export async function fetchDashboardData() {
  // Fetch all data in parallel for better performance
  const [
    pipelineStats,
    activePipelinesCount,
    connectorCount,
    scheduleCount,
    executionStats,
    recentExecutions
  ] = await Promise.all([
    fetchPipelineStats(),
    fetchActivePipelinesCount(),
    fetchConnectorCount(),
    fetchScheduleCount(),
    fetchExecutionStats(),
    fetchRecentExecutions(5)
  ])

  // Build stats object
  const stats = {
    totalPipelines: pipelineStats.totalCount,
    activePipelines: activePipelinesCount,
    connectors: connectorCount,
    schedules: scheduleCount,
    // Execution stats
    totalExecutions: executionStats.totalExecutions,
    runningExecutions: executionStats.runningExecutions,
    completedExecutions: executionStats.completedExecutions,
    failedExecutions: executionStats.failedExecutions,
    cancelledExecutions: executionStats.cancelledExecutions,
    successRate: executionStats.successRate,
    averageDurationMs: executionStats.averageDurationMs,
    totalRecordsProcessed: executionStats.totalRecordsProcessed,
    lastExecutionTime: executionStats.lastExecutionTime
  }

  // Build status distribution from execution stats
  const statusDistribution = [
    { name: 'Completed', count: executionStats.completedExecutions || 0 },
    { name: 'Running', count: executionStats.runningExecutions || 0 },
    { name: 'Failed', count: executionStats.failedExecutions || 0 },
    { name: 'Cancelled', count: executionStats.cancelledExecutions || 0 }
  ]

  // Map recent executions to consistent format
  const recentExecutionsData = recentExecutions.map(exec => ({
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
  }))

  return {
    stats,
    statusDistribution,
    recentExecutions: recentExecutionsData
  }
}

/**
 * Fetches only execution statistics (for quick refresh)
 * @returns {Promise<Object>} Execution statistics
 */
export async function refreshExecutionStats() {
  return await fetchExecutionStats()
}

/**
 * Fetches only recent executions (for quick refresh)
 * @param {number} limit - Number of recent executions to fetch
 * @returns {Promise<Array>} List of recent executions
 */
export async function refreshRecentExecutions(limit = 5) {
  return await fetchRecentExecutions(limit)
}