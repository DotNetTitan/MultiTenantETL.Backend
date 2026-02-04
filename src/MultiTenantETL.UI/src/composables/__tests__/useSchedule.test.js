import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest'
import { useSchedule } from '@/composables/useSchedule'
import {
  fetchSchedules,
  fetchScheduleById,
  fetchScheduleByPipelineId,
  createSchedule,
  updateSchedule,
  deleteSchedule,
  enableSchedule,
  disableSchedule,
  triggerSchedule,
  validateCronExpression
} from '@/services/scheduleService'

// Mock schedule service
vi.mock('@/services/scheduleService', () => ({
  fetchSchedules: vi.fn(),
  fetchScheduleById: vi.fn(),
  fetchScheduleByPipelineId: vi.fn(),
  createSchedule: vi.fn(),
  updateSchedule: vi.fn(),
  deleteSchedule: vi.fn(),
  enableSchedule: vi.fn(),
  disableSchedule: vi.fn(),
  triggerSchedule: vi.fn(),
  validateCronExpression: vi.fn()
}))

describe('useSchedule Composable', () => {
  let composable

  const mockSchedule = {
    id: 'schedule-1',
    pipelineId: 'pipeline-1',
    name: 'Daily ETL',
    description: 'Daily data extraction',
    cronExpression: '0 0 0 * * ?',
    timezone: 'UTC',
    isActive: true,
    lastRunAt: '2024-01-15T00:00:00Z',
    lastRunStatus: 'Completed',
    consecutiveFailures: 0,
    createdAt: '2024-01-01T00:00:00Z',
    updatedAt: '2024-01-01T00:00:00Z'
  }

  const mockSchedules = [
    mockSchedule,
    {
      id: 'schedule-2',
      pipelineId: 'pipeline-2',
      name: 'Hourly Sync',
      description: 'Hourly data sync',
      cronExpression: '0 0 * * * ?',
      timezone: 'America/New_York',
      isActive: false,
      lastRunAt: '2024-01-14T12:00:00Z',
      lastRunStatus: 'Failed',
      consecutiveFailures: 2,
      createdAt: '2024-01-01T00:00:00Z',
      updatedAt: '2024-01-01T00:00:00Z'
    }
  ]

  const mockPagination = {
    page: 1,
    pageSize: 20,
    totalCount: 2,
    totalPages: 1
  }

  const mockSchedulesResponse = {
    schedules: mockSchedules,
    ...mockPagination
  }

  const mockCronValidation = {
    isValid: true,
    errorMessage: null,
    description: 'At 00:00:00am every day',
    nextExecutions: [
      '2024-01-16T00:00:00Z',
      '2024-01-17T00:00:00Z',
      '2024-01-18T00:00:00Z'
    ]
  }

  beforeEach(() => {
    vi.clearAllMocks()
    // Create a fresh composable instance for each test
    composable = useSchedule()
    // Reset state for clean test isolation
    composable.reset()
  })

  describe('Initial State', () => {
    it('should initialize with empty state', () => {
      expect(composable.schedules.value).toEqual([])
      expect(composable.currentSchedule.value).toBe(null)
      expect(composable.loading.value).toBe(false)
      expect(composable.saving.value).toBe(false)
      expect(composable.error.value).toBe(null)
      expect(composable.validating.value).toBe(false)
      expect(composable.pagination.value).toEqual({
        page: 1,
        pageSize: 20,
        totalCount: 0,
        totalPages: 0
      })
      expect(composable.cronValidation.value).toEqual({
        isValid: null,
        errorMessage: null,
        description: null,
        nextExecutions: []
      })
    })
  })

  describe('loadSchedules', () => {
    it('should load schedules successfully', async () => {
      fetchSchedules.mockResolvedValue(mockSchedulesResponse)

      const result = await composable.loadSchedules()

      expect(fetchSchedules).toHaveBeenCalledWith({
        page: 1,
        pageSize: 20
      })
      expect(composable.schedules.value).toEqual(mockSchedules)
      expect(composable.pagination.value).toEqual(mockPagination)
      expect(composable.loading.value).toBe(false)
      expect(composable.error.value).toBe(null)
    })

    it('should load schedules with custom parameters', async () => {
      fetchSchedules.mockResolvedValue(mockSchedulesResponse)
      const params = { page: 2, pageSize: 10, search: 'test' }

      await composable.loadSchedules(params)

      expect(fetchSchedules).toHaveBeenCalledWith({
        page: 2,
        pageSize: 10,
        search: 'test'
      })
    })

    it('should handle API errors', async () => {
      const error = new Error('API Error')
      fetchSchedules.mockRejectedValue(error)

      await composable.loadSchedules()

      expect(composable.error.value).toBe('API Error')
      expect(composable.loading.value).toBe(false)
      expect(composable.schedules.value).toEqual([])
    })

    it('should set loading state correctly', async () => {
      fetchSchedules.mockImplementation(() => new Promise(resolve => {
        setTimeout(() => resolve(mockSchedulesResponse), 10)
      }))

      const promise = composable.loadSchedules()

      expect(composable.loading.value).toBe(true)

      await promise

      expect(composable.loading.value).toBe(false)
    })
  })

  describe('loadSchedule', () => {
    it('should load a single schedule successfully', async () => {
      fetchScheduleById.mockResolvedValue(mockSchedule)

      const result = await composable.loadSchedule('schedule-1')

      expect(fetchScheduleById).toHaveBeenCalledWith('schedule-1')
      expect(result).toEqual(mockSchedule)
      expect(composable.currentSchedule.value).toEqual(mockSchedule)
      expect(composable.loading.value).toBe(false)
      expect(composable.error.value).toBe(null)
    })

    it('should handle API errors', async () => {
      const error = new Error('Schedule not found')
      fetchScheduleById.mockRejectedValue(error)

      const result = await composable.loadSchedule('invalid-id')

      expect(result).toBe(null)
      expect(composable.error.value).toBe('Schedule not found')
      expect(composable.loading.value).toBe(false)
      expect(composable.currentSchedule.value).toBe(null)
    })

    it('should set loading state correctly', async () => {
      fetchScheduleById.mockImplementation(() => new Promise(resolve => {
        setTimeout(() => resolve(mockSchedule), 10)
      }))

      const promise = composable.loadSchedule('schedule-1')

      expect(composable.loading.value).toBe(true)

      await promise

      expect(composable.loading.value).toBe(false)
    })
  })

  describe('loadScheduleForPipeline', () => {
    it('should load schedule for pipeline successfully', async () => {
      fetchScheduleByPipelineId.mockResolvedValue(mockSchedule)

      const result = await composable.loadScheduleForPipeline('pipeline-1')

      expect(fetchScheduleByPipelineId).toHaveBeenCalledWith('pipeline-1')
      expect(result).toEqual(mockSchedule)
      expect(composable.currentSchedule.value).toEqual(mockSchedule)
      expect(composable.loading.value).toBe(false)
      expect(composable.error.value).toBe(null)
    })

    it('should handle API errors', async () => {
      const error = new Error('Pipeline schedule not found')
      fetchScheduleByPipelineId.mockRejectedValue(error)

      const result = await composable.loadScheduleForPipeline('invalid-pipeline')

      expect(result).toBe(null)
      expect(composable.error.value).toBe('Pipeline schedule not found')
      expect(composable.loading.value).toBe(false)
      expect(composable.currentSchedule.value).toBe(null)
    })
  })

  describe('saveNewSchedule', () => {
    it('should create a new schedule successfully', async () => {
      const newScheduleData = {
        pipelineId: 'pipeline-1',
        name: 'New Schedule',
        cronExpression: '0 0 0 * * ?',
        timezone: 'UTC'
      }
      createSchedule.mockResolvedValue(mockSchedule)

      const result = await composable.saveNewSchedule(newScheduleData)

      expect(createSchedule).toHaveBeenCalledWith(newScheduleData)
      expect(result).toEqual(mockSchedule)
      expect(composable.schedules.value).toHaveLength(1)
      expect(composable.schedules.value[0]).toEqual(mockSchedule)
      expect(composable.saving.value).toBe(false)
      expect(composable.error.value).toBe(null)
    })

    it('should handle API errors', async () => {
      const error = new Error('Validation failed')
      createSchedule.mockRejectedValue(error)

      await expect(composable.saveNewSchedule({})).rejects.toThrow('Validation failed')

      expect(composable.error.value).toBe('Validation failed')
      expect(composable.saving.value).toBe(false)
    })

    it('should set saving state correctly', async () => {
      createSchedule.mockImplementation(() => new Promise(resolve => {
        setTimeout(() => resolve(mockSchedule), 10)
      }))

      const promise = composable.saveNewSchedule({})

      expect(composable.saving.value).toBe(true)

      await promise

      expect(composable.saving.value).toBe(false)
    })
  })

  describe('saveSchedule', () => {
    it('should update an existing schedule successfully', async () => {
      const updatedSchedule = { ...mockSchedule, name: 'Updated Name' }
      updateSchedule.mockResolvedValue(updatedSchedule)

      // Pre-populate schedules
      composable.schedules.value = [mockSchedule]

      const result = await composable.saveSchedule('schedule-1', { name: 'Updated Name' })

      expect(updateSchedule).toHaveBeenCalledWith('schedule-1', { name: 'Updated Name' })
      expect(result).toEqual(updatedSchedule)
      expect(composable.schedules.value[0]).toEqual(updatedSchedule)
      expect(composable.saving.value).toBe(false)
      expect(composable.error.value).toBe(null)
    })

    it('should update currentSchedule if it matches the updated ID', async () => {
      const updatedSchedule = { ...mockSchedule, name: 'Updated Name' }
      updateSchedule.mockResolvedValue(updatedSchedule)

      // Set current schedule
      composable.currentSchedule.value = mockSchedule

      await composable.saveSchedule('schedule-1', { name: 'Updated Name' })

      expect(composable.currentSchedule.value).toEqual(updatedSchedule)
    })

    it('should handle API errors', async () => {
      const error = new Error('Update failed')
      updateSchedule.mockRejectedValue(error)

      await expect(composable.saveSchedule('schedule-1', {})).rejects.toThrow('Update failed')

      expect(composable.error.value).toBe('Update failed')
      expect(composable.saving.value).toBe(false)
    })
  })

  describe('removeSchedule', () => {
    it('should delete a schedule successfully', async () => {
      deleteSchedule.mockResolvedValue()

      // Pre-populate schedules
      composable.schedules.value = mockSchedules

      await composable.removeSchedule('schedule-1')

      expect(deleteSchedule).toHaveBeenCalledWith('schedule-1')
      expect(composable.schedules.value).toHaveLength(1)
      expect(composable.schedules.value[0].id).toBe('schedule-2')
      expect(composable.saving.value).toBe(false)
      expect(composable.error.value).toBe(null)
    })

    it('should clear currentSchedule if it matches the deleted ID', async () => {
      deleteSchedule.mockResolvedValue()

      // Set current schedule
      composable.currentSchedule.value = mockSchedule

      await composable.removeSchedule('schedule-1')

      expect(composable.currentSchedule.value).toBe(null)
    })

    it('should handle API errors', async () => {
      const error = new Error('Delete failed')
      deleteSchedule.mockRejectedValue(error)

      await expect(composable.removeSchedule('schedule-1')).rejects.toThrow('Delete failed')

      expect(composable.error.value).toBe('Delete failed')
      expect(composable.saving.value).toBe(false)
    })
  })

  describe('activateSchedule', () => {
    it('should enable a schedule successfully', async () => {
      const enabledSchedule = { ...mockSchedule, isActive: true }
      enableSchedule.mockResolvedValue(enabledSchedule)

      // Pre-populate schedules
      composable.schedules.value = [{ ...mockSchedule, isActive: false }]

      const result = await composable.activateSchedule('schedule-1')

      expect(enableSchedule).toHaveBeenCalledWith('schedule-1')
      expect(result).toEqual(enabledSchedule)
      expect(composable.schedules.value[0]).toEqual(enabledSchedule)
      expect(composable.saving.value).toBe(false)
      expect(composable.error.value).toBe(null)
    })

    it('should handle API errors', async () => {
      const error = new Error('Enable failed')
      enableSchedule.mockRejectedValue(error)

      await expect(composable.activateSchedule('schedule-1')).rejects.toThrow('Enable failed')

      expect(composable.error.value).toBe('Enable failed')
      expect(composable.saving.value).toBe(false)
    })
  })

  describe('deactivateSchedule', () => {
    it('should disable a schedule successfully', async () => {
      const disabledSchedule = { ...mockSchedule, isActive: false }
      disableSchedule.mockResolvedValue(disabledSchedule)

      // Pre-populate schedules
      composable.schedules.value = [mockSchedule]

      const result = await composable.deactivateSchedule('schedule-1')

      expect(disableSchedule).toHaveBeenCalledWith('schedule-1')
      expect(result).toEqual(disabledSchedule)
      expect(composable.schedules.value[0]).toEqual(disabledSchedule)
      expect(composable.saving.value).toBe(false)
      expect(composable.error.value).toBe(null)
    })

    it('should handle API errors', async () => {
      const error = new Error('Disable failed')
      disableSchedule.mockRejectedValue(error)

      await expect(composable.deactivateSchedule('schedule-1')).rejects.toThrow('Disable failed')

      expect(composable.error.value).toBe('Disable failed')
      expect(composable.saving.value).toBe(false)
    })
  })

  describe('toggleScheduleActive', () => {
    it('should activate an inactive schedule', async () => {
      const inactiveSchedule = { ...mockSchedule, isActive: false }
      const activatedSchedule = { ...mockSchedule, isActive: true }
      enableSchedule.mockResolvedValue(activatedSchedule)

      composable.schedules.value = [inactiveSchedule]

      const result = await composable.toggleScheduleActive('schedule-1')

      expect(enableSchedule).toHaveBeenCalledWith('schedule-1')
      expect(result).toEqual(activatedSchedule)
    })

    it('should deactivate an active schedule', async () => {
      const activatedSchedule = { ...mockSchedule, isActive: false }
      disableSchedule.mockResolvedValue(activatedSchedule)

      composable.schedules.value = [mockSchedule]

      const result = await composable.toggleScheduleActive('schedule-1')

      expect(disableSchedule).toHaveBeenCalledWith('schedule-1')
      expect(result).toEqual(activatedSchedule)
    })

    it('should do nothing if schedule not found', async () => {
      const result = await composable.toggleScheduleActive('non-existent')

      expect(result).toBeUndefined()
      expect(enableSchedule).not.toHaveBeenCalled()
      expect(disableSchedule).not.toHaveBeenCalled()
    })
  })

  describe('runScheduleNow', () => {
    it('should trigger a schedule successfully', async () => {
      const triggeredSchedule = { ...mockSchedule, lastRunAt: new Date().toISOString() }
      triggerSchedule.mockResolvedValue(triggeredSchedule)

      // Pre-populate schedules
      composable.schedules.value = [mockSchedule]

      const result = await composable.runScheduleNow('schedule-1')

      expect(triggerSchedule).toHaveBeenCalledWith('schedule-1')
      expect(result).toEqual(triggeredSchedule)
      expect(composable.schedules.value[0]).toEqual(triggeredSchedule)
      expect(composable.saving.value).toBe(false)
      expect(composable.error.value).toBe(null)
    })

    it('should handle API errors', async () => {
      const error = new Error('Trigger failed')
      triggerSchedule.mockRejectedValue(error)

      await expect(composable.runScheduleNow('schedule-1')).rejects.toThrow('Trigger failed')

      expect(composable.error.value).toBe('Trigger failed')
      expect(composable.saving.value).toBe(false)
    })
  })

  describe('validateCron', () => {
    it('should validate a cron expression successfully', async () => {
      validateCronExpression.mockResolvedValue(mockCronValidation)

      const result = await composable.validateCron('0 0 0 * * ?', 'UTC')

      expect(validateCronExpression).toHaveBeenCalledWith('0 0 0 * * ?', 'UTC')
      expect(result).toEqual(mockCronValidation)
      expect(composable.cronValidation.value).toEqual(mockCronValidation)
      expect(composable.validating.value).toBe(false)
    })

    it('should handle validation errors', async () => {
      const error = new Error('Invalid cron expression')
      validateCronExpression.mockRejectedValue(error)

      const result = await composable.validateCron('invalid', 'UTC')

      expect(result).toEqual({
        isValid: false,
        errorMessage: 'Invalid cron expression',
        description: null,
        nextExecutions: []
      })
      expect(composable.cronValidation.value).toEqual({
        isValid: false,
        errorMessage: 'Invalid cron expression',
        description: null,
        nextExecutions: []
      })
      expect(composable.validating.value).toBe(false)
    })

    it('should use UTC as default timezone', async () => {
      validateCronExpression.mockResolvedValue(mockCronValidation)

      await composable.validateCron('0 0 0 * * ?')

      expect(validateCronExpression).toHaveBeenCalledWith('0 0 0 * * ?', 'UTC')
    })

    it('should set validating state correctly', async () => {
      validateCronExpression.mockImplementation(() => new Promise(resolve => {
        setTimeout(() => resolve(mockCronValidation), 10)
      }))

      const promise = composable.validateCron('0 0 0 * * ?')

      expect(composable.validating.value).toBe(true)

      await promise

      expect(composable.validating.value).toBe(false)
    })
  })

  describe('getStatusColor', () => {
    it('should return grey for inactive schedules', () => {
      const inactiveSchedule = { ...mockSchedule, isActive: false }
      expect(composable.getStatusColor(inactiveSchedule)).toBe('grey')
    })

    it('should return error for failed last run', () => {
      const failedSchedule = { ...mockSchedule, lastRunStatus: 'Failed' }
      expect(composable.getStatusColor(failedSchedule)).toBe('error')
    })

    it('should return success for completed last run', () => {
      const completedSchedule = { ...mockSchedule, lastRunStatus: 'Completed' }
      expect(composable.getStatusColor(completedSchedule)).toBe('success')
    })

    it('should return warning for consecutive failures', () => {
      const failingSchedule = { ...mockSchedule, lastRunStatus: null, consecutiveFailures: 1 }
      expect(composable.getStatusColor(failingSchedule)).toBe('warning')
    })

    it('should return success for active schedules with no issues', () => {
      expect(composable.getStatusColor(mockSchedule)).toBe('success')
    })
  })

  describe('getStatusIcon', () => {
    it('should return pause icon for inactive schedules', () => {
      const inactiveSchedule = { ...mockSchedule, isActive: false }
      expect(composable.getStatusIcon(inactiveSchedule)).toBe('mdi-pause-circle-outline')
    })

    it('should return alert icon for failed last run', () => {
      const failedSchedule = { ...mockSchedule, lastRunStatus: 'Failed' }
      expect(composable.getStatusIcon(failedSchedule)).toBe('mdi-alert-circle')
    })

    it('should return check icon for completed last run', () => {
      const completedSchedule = { ...mockSchedule, lastRunStatus: 'Completed' }
      expect(composable.getStatusIcon(completedSchedule)).toBe('mdi-check-circle')
    })

    it('should return alert icon for consecutive failures', () => {
      const failingSchedule = { ...mockSchedule, lastRunStatus: null, consecutiveFailures: 1 }
      expect(composable.getStatusIcon(failingSchedule)).toBe('mdi-alert')
    })

    it('should return timer icon for active schedules with no issues', () => {
      const cleanSchedule = { ...mockSchedule, lastRunStatus: null, consecutiveFailures: 0 }
      expect(composable.getStatusIcon(cleanSchedule)).toBe('mdi-timer-outline')
    })
  })

  describe('formatDate', () => {
    it('should format valid date strings', () => {
      const dateString = '2024-01-15T10:30:00Z'
      const result = composable.formatDate(dateString)
      expect(result).toContain('1/15/2024') // Date format may vary by locale
    })

    it('should return dash for null/undefined dates', () => {
      expect(composable.formatDate(null)).toBe('-')
      expect(composable.formatDate(undefined)).toBe('-')
      expect(composable.formatDate('')).toBe('-')
    })
  })

  describe('formatRelativeTime', () => {
    beforeEach(() => {
      // Mock current time
      vi.useFakeTimers()
      vi.setSystemTime(new Date('2024-01-15T12:00:00Z'))
    })

    afterEach(() => {
      vi.useRealTimers()
    })

    it('should format past times correctly', () => {
      expect(composable.formatRelativeTime('2024-01-15T11:45:00Z')).toBe('15 minutes ago')
      expect(composable.formatRelativeTime('2024-01-15T10:00:00Z')).toBe('2 hours ago')
      expect(composable.formatRelativeTime('2024-01-13T12:00:00Z')).toBe('2 days ago')
    })

    it('should format future times correctly', () => {
      expect(composable.formatRelativeTime('2024-01-15T12:15:00Z')).toBe('in 15 minutes')
      expect(composable.formatRelativeTime('2024-01-15T14:00:00Z')).toBe('in 2 hours')
      expect(composable.formatRelativeTime('2024-01-17T12:00:00Z')).toBe('in 2 days')
    })

    it('should return dash for null/undefined dates', () => {
      expect(composable.formatRelativeTime(null)).toBe('-')
      expect(composable.formatRelativeTime(undefined)).toBe('-')
      expect(composable.formatRelativeTime('')).toBe('-')
    })
  })

  describe('createEmptySchedule', () => {
    it('should create an empty schedule object with defaults', () => {
      const emptySchedule = composable.createEmptySchedule()

      expect(emptySchedule).toEqual({
        pipelineId: null,
        cronExpression: '0 0 0 * * ?',
        timezone: 'UTC',
        description: '',
        isActive: true
      })
    })
  })

  describe('Computed Properties', () => {
    it('should compute hasSchedules correctly', () => {
      expect(composable.hasSchedules.value).toBe(false)

      composable.schedules.value = [mockSchedule]
      expect(composable.hasSchedules.value).toBe(true)
    })

    it('should compute activeSchedules correctly', () => {
      composable.schedules.value = mockSchedules
      expect(composable.activeSchedules.value).toHaveLength(1)
      expect(composable.activeSchedules.value[0].id).toBe('schedule-1')
    })

    it('should compute inactiveSchedules correctly', () => {
      composable.schedules.value = mockSchedules
      expect(composable.inactiveSchedules.value).toHaveLength(1)
      expect(composable.inactiveSchedules.value[0].id).toBe('schedule-2')
    })
  })

  describe('Constants', () => {
    it('should have commonTimezones defined', () => {
      expect(composable.commonTimezones).toBeDefined()
      expect(Array.isArray(composable.commonTimezones)).toBe(true)
      expect(composable.commonTimezones.length).toBeGreaterThan(0)
      expect(composable.commonTimezones[0]).toHaveProperty('title')
      expect(composable.commonTimezones[0]).toHaveProperty('value')
    })

    it('should have allTimezones defined', () => {
      expect(composable.allTimezones).toBeDefined()
      expect(Array.isArray(composable.allTimezones)).toBe(true)
    })

    it('should have cronPresets defined', () => {
      expect(composable.cronPresets).toBeDefined()
      expect(Array.isArray(composable.cronPresets)).toBe(true)
      expect(composable.cronPresets.length).toBeGreaterThan(0)
      expect(composable.cronPresets[0]).toHaveProperty('text')
      expect(composable.cronPresets[0]).toHaveProperty('value')
    })
  })

  describe('reset', () => {
    it('should reset all state to initial values', () => {
      // Set some state
      composable.schedules.value = mockSchedules
      composable.currentSchedule.value = mockSchedule
      composable.error.value = 'Test error'
      composable.pagination.value = { page: 2, pageSize: 10, totalCount: 50, totalPages: 5 }
      composable.cronValidation.value = mockCronValidation

      composable.reset()

      expect(composable.schedules.value).toEqual([])
      expect(composable.currentSchedule.value).toBe(null)
      expect(composable.error.value).toBe(null)
      expect(composable.pagination.value).toEqual({
        page: 1,
        pageSize: 20,
        totalCount: 0,
        totalPages: 0
      })
      expect(composable.cronValidation.value).toEqual({
        isValid: null,
        errorMessage: null,
        description: null,
        nextExecutions: []
      })
    })
  })
})