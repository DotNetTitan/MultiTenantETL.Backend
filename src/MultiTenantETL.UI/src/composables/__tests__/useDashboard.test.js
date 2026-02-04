import { describe, it, expect, beforeEach, vi } from 'vitest'
import { useDashboard } from '@/composables/useDashboard'
import { fetchDashboardData, refreshExecutionStats, refreshRecentExecutions } from '@/services/dashboardService'
import { useTenantStore } from '@/stores/tenant'

// Mock the dashboard service functions that are imported directly
vi.mock('@/services/dashboardService', () => ({
  fetchDashboardData: vi.fn(),
  refreshExecutionStats: vi.fn(),
  refreshRecentExecutions: vi.fn()
}))

// Mock the tenant store
const mockTenantStore = {
  currentTenantId: 'tenant-1',
  $subscribe: vi.fn()
}

vi.mock('@/stores/tenant', () => ({
  useTenantStore: vi.fn(() => mockTenantStore)
}))

describe('useDashboard Composable', () => {
  let composable

  beforeEach(() => {
    vi.clearAllMocks()
    composable = useDashboard()

    // Reset mocks
    fetchDashboardData.mockReset()
    refreshExecutionStats.mockReset()
    refreshRecentExecutions.mockReset()
  })

  describe('Initial State', () => {
    it('should initialize with correct default values', () => {
      expect(composable.loading.value).toBe(true)
      expect(composable.error.value).toBe(null)
      expect(composable.recentExecutions.value).toEqual([])
      expect(composable.stats.value).toEqual({
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
      })
      expect(composable.statusDistribution.value).toEqual([
        { name: 'Completed', count: 0 },
        { name: 'Running', count: 0 },
        { name: 'Failed', count: 0 },
        { name: 'Cancelled', count: 0 }
      ])
    })
  })

  describe('Computed Properties', () => {
    it('should format success rate correctly', () => {
      composable.stats.value.successRate = 85.7
      expect(composable.formattedSuccessRate.value).toBe('85.7%')

      composable.stats.value.successRate = null
      expect(composable.formattedSuccessRate.value).toBe('0%')
    })

    it('should format total records correctly', () => {
      composable.stats.value.totalRecordsProcessed = 1500000
      expect(composable.formattedTotalRecords.value).toBe('1.5M')

      composable.stats.value.totalRecordsProcessed = 2500
      expect(composable.formattedTotalRecords.value).toBe('2.5K')

      composable.stats.value.totalRecordsProcessed = 500
      expect(composable.formattedTotalRecords.value).toBe('500')
    })

    it('should format average duration correctly', () => {
      composable.stats.value.averageDurationMs = 3661000 // 1h 1m 1s
      expect(composable.formattedAverageDuration.value).toBe('1h 1m')

      composable.stats.value.averageDurationMs = 61000 // 1m 1s
      expect(composable.formattedAverageDuration.value).toBe('1m 1s')

      composable.stats.value.averageDurationMs = 5000 // 5s
      expect(composable.formattedAverageDuration.value).toBe('5s')

      composable.stats.value.averageDurationMs = 500 // 500ms
      expect(composable.formattedAverageDuration.value).toBe('500ms')
    })

    it('should detect running executions correctly', () => {
      composable.stats.value.runningExecutions = 0
      expect(composable.hasRunningExecutions.value).toBe(false)

      composable.stats.value.runningExecutions = 3
      expect(composable.hasRunningExecutions.value).toBe(true)
    })
  })

  describe('Utility Functions', () => {
    describe('getStatusColor', () => {
      it('should return correct colors for different statuses', () => {
        expect(composable.getStatusColor('completed')).toBe('success')
        expect(composable.getStatusColor('Completed')).toBe('success')
        expect(composable.getStatusColor('running')).toBe('info')
        expect(composable.getStatusColor('failed')).toBe('error')
        expect(composable.getStatusColor('cancelled')).toBe('warning')
        expect(composable.getStatusColor('unknown')).toBe('grey')
        expect(composable.getStatusColor(null)).toBe('grey')
      })
    })

    describe('getStatusIcon', () => {
      it('should return correct icons for different statuses', () => {
        expect(composable.getStatusIcon('completed')).toBe('mdi-check-circle')
        expect(composable.getStatusIcon('running')).toBe('mdi-progress-clock')
        expect(composable.getStatusIcon('failed')).toBe('mdi-alert-circle')
        expect(composable.getStatusIcon('cancelled')).toBe('mdi-cancel')
        expect(composable.getStatusIcon('unknown')).toBe('mdi-information')
      })
    })

    describe('formatDate', () => {
      it('should format date strings correctly', () => {
        const dateString = '2023-12-01T10:30:00Z'
        const result = composable.formatDate(dateString)
        expect(result).not.toBe('-')
        expect(typeof result).toBe('string')
      })

      it('should handle null/undefined dates', () => {
        expect(composable.formatDate(null)).toBe('-')
        expect(composable.formatDate(undefined)).toBe('-')
        expect(composable.formatDate('')).toBe('-')
      })
    })

    describe('formatRelativeTime', () => {
      it('should format relative time correctly', () => {
        const now = new Date()
        const fiveMinutesAgo = new Date(now.getTime() - 5 * 60 * 1000)
        const result = composable.formatRelativeTime(fiveMinutesAgo.toISOString())
        expect(result).toBe('5m ago')
      })

      it('should handle null/undefined dates', () => {
        expect(composable.formatRelativeTime(null)).toBe('-')
        expect(composable.formatRelativeTime(undefined)).toBe('-')
      })
    })

    describe('formatDuration', () => {
      it('should format durations correctly', () => {
        expect(composable.formatDuration(3661000)).toBe('1h 1m') // 1h 1m 1s
        expect(composable.formatDuration(61000)).toBe('1m 1s') // 1m 1s
        expect(composable.formatDuration(5000)).toBe('5s') // 5s
        expect(composable.formatDuration(500)).toBe('500ms') // 500ms
        expect(composable.formatDuration(0)).toBe('0ms') // 0ms
      })

      it('should handle null/undefined durations', () => {
        expect(composable.formatDuration(null)).toBe('-')
        expect(composable.formatDuration(undefined)).toBe('-')
      })
    })
  })

  describe('loadDashboardData', () => {
    it('should successfully load dashboard data', async () => {
      const mockData = {
        stats: {
          totalPipelines: 10,
          activePipelines: 8,
          connectors: 5,
          schedules: 3,
          totalExecutions: 150,
          runningExecutions: 3,
          completedExecutions: 120,
          failedExecutions: 15,
          cancelledExecutions: 12,
          successRate: 80.0,
          averageDurationMs: 45000,
          totalRecordsProcessed: 125000,
          lastExecutionTime: '2023-12-01T10:30:00Z'
        },
        statusDistribution: [
          { name: 'Completed', count: 120 },
          { name: 'Running', count: 3 },
          { name: 'Failed', count: 15 },
          { name: 'Cancelled', count: 12 }
        ],
        recentExecutions: [
          {
            id: 'exec-1',
            pipelineId: 'pipe-1',
            pipelineName: 'Test Pipeline',
            startTime: '2023-12-01T10:00:00Z',
            endTime: '2023-12-01T10:30:00Z',
            duration: '30m',
            durationMs: 1800000,
            status: 'completed',
            rowsProcessed: 1000,
            progressPercent: 100,
            triggeredBy: 'user@example.com'
          }
        ]
      }

      fetchDashboardData.mockResolvedValue(mockData)

      await composable.loadDashboardData()

      expect(fetchDashboardData).toHaveBeenCalled()
      expect(composable.loading.value).toBe(false)
      expect(composable.error.value).toBe(null)
      expect(composable.stats.value).toEqual(mockData.stats)
      expect(composable.statusDistribution.value).toEqual(mockData.statusDistribution)
      expect(composable.recentExecutions.value).toEqual(mockData.recentExecutions)
    })

    it('should handle errors when loading dashboard data', async () => {
      const mockError = new Error('Failed to load dashboard data')
      fetchDashboardData.mockRejectedValue(mockError)

      await composable.loadDashboardData()

      expect(fetchDashboardData).toHaveBeenCalled()
      expect(composable.loading.value).toBe(false)
      expect(composable.error.value).toBe('Failed to load dashboard data')
    })

    it('should set loading state during data load', async () => {
      fetchDashboardData.mockResolvedValue({
        stats: {},
        statusDistribution: [],
        recentExecutions: []
      })

      const promise = composable.loadDashboardData()

      expect(composable.loading.value).toBe(true)

      await promise

      expect(composable.loading.value).toBe(false)
    })
  })

  describe('refreshStats', () => {
    it('should successfully refresh execution stats', async () => {
      const mockStats = {
        totalExecutions: 160,
        runningExecutions: 2,
        completedExecutions: 130,
        failedExecutions: 18,
        cancelledExecutions: 10,
        successRate: 81.3,
        averageDurationMs: 42000,
        totalRecordsProcessed: 140000,
        lastExecutionTime: '2023-12-01T11:00:00Z'
      }

      refreshExecutionStats.mockResolvedValue(mockStats)

      await composable.refreshStats()

      expect(refreshExecutionStats).toHaveBeenCalled()
      expect(composable.stats.value.totalExecutions).toBe(160)
      expect(composable.stats.value.runningExecutions).toBe(2)
      expect(composable.stats.value.successRate).toBe(81.3)
      expect(composable.statusDistribution.value).toEqual([
        { name: 'Completed', count: 130 },
        { name: 'Running', count: 2 },
        { name: 'Failed', count: 18 },
        { name: 'Cancelled', count: 10 }
      ])
    })

    it('should handle errors when refreshing stats', async () => {
      const mockError = new Error('Failed to refresh stats')
      refreshExecutionStats.mockRejectedValue(mockError)

      // Should not throw, just log error
      await expect(composable.refreshStats()).resolves.toBeUndefined()
      expect(refreshExecutionStats).toHaveBeenCalled()
    })
  })

  describe('refreshExecutions', () => {
    it('should successfully refresh recent executions', async () => {
      const mockExecutions = [
        {
          id: 'exec-1',
          pipelineId: 'pipe-1',
          pipelineName: 'Pipeline 1',
          startTime: '2023-12-01T10:00:00Z',
          endTime: '2023-12-01T10:30:00Z',
          duration: '30m',
          durationMs: 1800000,
          status: 'completed',
          recordsProcessed: 1000,
          progressPercent: 100,
          triggeredBy: 'user@example.com'
        },
        {
          id: 'exec-2',
          pipelineId: 'pipe-2',
          pipelineName: 'Pipeline 2',
          startTime: '2023-12-01T09:00:00Z',
          status: 'running',
          recordsProcessed: 500,
          progressPercent: 50,
          triggeredBy: 'user@example.com'
        }
      ]

      refreshRecentExecutions.mockResolvedValue(mockExecutions)

      await composable.refreshExecutions()

      expect(refreshRecentExecutions).toHaveBeenCalledWith(5)
      expect(composable.recentExecutions.value).toHaveLength(2)
      expect(composable.recentExecutions.value[0].id).toBe('exec-1')
      expect(composable.recentExecutions.value[0].pipelineName).toBe('Pipeline 1')
      expect(composable.recentExecutions.value[1].status).toBe('running')
    })

    it('should handle errors when refreshing executions', async () => {
      const mockError = new Error('Failed to refresh executions')
      refreshRecentExecutions.mockRejectedValue(mockError)

      // Should not throw, just log error
      await expect(composable.refreshExecutions()).resolves.toBeUndefined()
      expect(refreshRecentExecutions).toHaveBeenCalledWith(5)
    })
  })

  describe('setupTenantSubscription', () => {
    it('should setup tenant subscription', () => {
      composable.setupTenantSubscription()

      expect(mockTenantStore.$subscribe).toHaveBeenCalled()
    })
  })

  describe('Error Handling', () => {
    it('should clear previous errors on new operations', async () => {
      // Set initial error
      composable.error.value = 'Previous error'

      const mockError = new Error('New error')
      fetchDashboardData.mockRejectedValue(mockError)

      await composable.loadDashboardData()

      expect(composable.error.value).toBe('New error')
    })
  })
})