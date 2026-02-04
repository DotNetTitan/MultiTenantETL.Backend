import { describe, it, expect, beforeEach, vi } from 'vitest'
import { ref, computed } from 'vue'
import { usePipeline } from '@/composables/usePipeline'
import { useTenantStore } from '@/stores/tenant'
import {
  fetchPipelines,
  fetchPipelineById,
  savePipeline as apiSavePipeline,
  deletePipeline as apiDeletePipeline,
  executePipeline as apiExecutePipeline,
  togglePipelineStatus as apiTogglePipelineStatus
} from '@/services/pipelineService'

// Mock dependencies
vi.mock('vue-i18n', () => ({
  useI18n: () => ({
    t: vi.fn((key) => key)
  })
}))

vi.mock('@/composables/useTransformation', () => ({
  useTransformation: () => ({
    validateTransformation: vi.fn(() => ({ isValid: true, errors: [] })),
    getOutputSchema: vi.fn(() => ({ columns: [] }))
  })
}))

vi.mock('@/composables/useConnector', () => ({
  useConnector: () => ({
    detectSchema: vi.fn(() => ({ columns: [] }))
  })
}))

vi.mock('@/stores/tenant', () => ({
  useTenantStore: vi.fn(() => ({
    currentTenantId: 'tenant-1',
    $subscribe: vi.fn()
  }))
}))

// Mock pipeline service functions
vi.mock('@/services/pipelineService', () => ({
  fetchPipelines: vi.fn(),
  fetchPipelineById: vi.fn(),
  savePipeline: vi.fn(),
  deletePipeline: vi.fn(),
  executePipeline: vi.fn(),
  togglePipelineStatus: vi.fn()
}))

describe('usePipeline Composable', () => {
  let composable
  let mockTenantStore

  const mockPipelines = [
    {
      id: '1',
      name: 'Test Pipeline 1',
      description: 'A test pipeline',
      sourceConnectorName: 'Source DB',
      destinationConnectorName: 'Dest DB',
      status: 'Idle',
      isActive: true,
      lastRunAt: '2024-01-01T00:00:00Z'
    },
    {
      id: '2',
      name: 'Test Pipeline 2',
      description: 'Another test pipeline',
      sourceConnectorName: 'Source API',
      destinationConnectorName: 'Dest File',
      status: 'Running',
      isActive: false,
      lastRunAt: '2024-01-02T00:00:00Z'
    }
  ]

  const mockPipeline = {
    id: '1',
    name: 'Test Pipeline',
    description: 'A test pipeline',
    sourceId: 'source-1',
    destinationId: 'dest-1',
    transformations: [],
    fieldMappings: [],
    isScheduled: false,
    isActive: true,
    schedule: {
      frequency: 'Daily',
      time: '00:00',
      cronExpression: '0 0 * * *'
    }
  }

  beforeEach(() => {
    vi.clearAllMocks()

    // Reset composable state
    composable = usePipeline()

    // Get the mocked tenant store
    mockTenantStore = {
      currentTenantId: 'tenant-1',
      $subscribe: vi.fn()
    }
  })

  describe('Initial State', () => {
    it('should initialize with correct default values', () => {
      expect(composable.error.value).toBe(null)
      expect(composable.pipelines.value).toEqual([])
      expect(composable.loading.value).toBe(false)
      expect(composable.savingPipeline.value).toBe(false)
      expect(composable.deletingPipeline.value).toBe(false)
      expect(composable.search.value).toBe('')
      expect(composable.statusFilter.value).toBe('All')
      expect(composable.sortBy.value).toBe('name_asc')
    })

    it('should have computed status options', () => {
      expect(composable.statusOptions.value).toEqual([
        { title: 'filters.allStatuses', value: 'All' },
        { title: 'executions.idle', value: 'Idle' },
        { title: 'executions.running', value: 'Running' },
        { title: 'executions.failed', value: 'Failed' }
      ])
    })

    it('should have computed sort options', () => {
      expect(composable.sortOptions.value).toEqual([
        { title: 'filters.nameAsc', value: 'name_asc' },
        { title: 'filters.nameDesc', value: 'name_desc' },
        { title: 'filters.lastRunNewest', value: 'lastRun_desc' },
        { title: 'filters.lastRunOldest', value: 'lastRun_asc' }
      ])
    })
  })

  describe('loadPipelines', () => {
    it('should load pipelines successfully', async () => {
      fetchPipelines.mockResolvedValue(mockPipelines)

      await composable.loadPipelines()

      expect(fetchPipelines).toHaveBeenCalledWith({
        search: '',
        status: 'All',
        sortBy: 'name_asc'
      })
      expect(composable.pipelines.value).toEqual([
        {
          ...mockPipelines[0],
          sourceName: 'Source DB',
          destinationName: 'Dest DB'
        },
        {
          ...mockPipelines[1],
          sourceName: 'Source API',
          destinationName: 'Dest File'
        }
      ])
      expect(composable.loading.value).toBe(false)
      expect(composable.error.value).toBe(null)
    })

    it('should handle API errors', async () => {
      const error = new Error('API Error')
      fetchPipelines.mockRejectedValue(error)

      const consoleSpy = vi.spyOn(console, 'error').mockImplementation(() => {})

      await composable.loadPipelines()

      expect(consoleSpy).toHaveBeenCalledWith('Error loading pipelines:', error)
      expect(composable.error.value).toBe('API Error')
      expect(composable.loading.value).toBe(false)

      consoleSpy.mockRestore()
    })

    it('should apply filters when loading pipelines', async () => {
      composable.search.value = 'test'
      composable.statusFilter.value = 'Running'
      composable.sortBy.value = 'lastRun_desc'

      fetchPipelines.mockResolvedValue([mockPipelines[1]])

      await composable.loadPipelines()

      expect(fetchPipelines).toHaveBeenCalledWith({
        search: 'test',
        status: 'Running',
        sortBy: 'lastRun_desc'
      })
    })

    it('should set loading state correctly', async () => {
      fetchPipelines.mockImplementation(() => new Promise(resolve => {
        setTimeout(() => resolve(mockPipelines), 100)
      }))

      const promise = composable.loadPipelines()

      expect(composable.loading.value).toBe(true)

      await promise

      expect(composable.loading.value).toBe(false)
    })
  })

  describe('getPipeline', () => {
    it('should fetch pipeline by ID successfully', async () => {
      fetchPipelineById.mockResolvedValue(mockPipeline)

      const result = await composable.getPipeline('1')

      expect(fetchPipelineById).toHaveBeenCalledWith('1')
      expect(result).toEqual(mockPipeline)
      expect(composable.loading.value).toBe(false)
    })

    it('should handle API errors and return null', async () => {
      const error = new Error('API Error')
      fetchPipelineById.mockRejectedValue(error)

      const consoleSpy = vi.spyOn(console, 'error').mockImplementation(() => {})

      const result = await composable.getPipeline('1')

      expect(consoleSpy).toHaveBeenCalledWith('Error getting pipeline 1:', error)
      expect(result).toBe(null)
      expect(composable.error.value).toBe('API Error')

      consoleSpy.mockRestore()
    })
  })

  describe('savePipeline', () => {
    it('should save pipeline successfully', async () => {
      apiSavePipeline.mockResolvedValue(mockPipeline)

      const result = await composable.savePipeline(mockPipeline)

      expect(apiSavePipeline).toHaveBeenCalledWith(mockPipeline)
      expect(result).toEqual(mockPipeline)
      expect(composable.savingPipeline.value).toBe(false)
    })

    it('should handle API errors', async () => {
      const error = new Error('API Error')
      apiSavePipeline.mockRejectedValue(error)

      const consoleSpy = vi.spyOn(console, 'error').mockImplementation(() => {})

      await expect(composable.savePipeline(mockPipeline)).rejects.toThrow('API Error')

      expect(consoleSpy).toHaveBeenCalledWith('Error saving pipeline:', error)
      expect(composable.error.value).toBe('API Error')
      expect(composable.savingPipeline.value).toBe(false)

      consoleSpy.mockRestore()
    })

    it('should set saving state correctly', async () => {
      apiSavePipeline.mockImplementation(() => new Promise(resolve => {
        setTimeout(() => resolve(mockPipeline), 100)
      }))

      const promise = composable.savePipeline(mockPipeline)

      expect(composable.savingPipeline.value).toBe(true)

      await promise

      expect(composable.savingPipeline.value).toBe(false)
    })
  })

  describe('deletePipeline', () => {
    beforeEach(() => {
      composable.pipelines.value = [...mockPipelines]
    })

    it('should delete pipeline successfully', async () => {
      apiDeletePipeline.mockResolvedValue(true)

      const result = await composable.deletePipeline('1')

      expect(apiDeletePipeline).toHaveBeenCalledWith('1')
      expect(result).toBe(true)
      expect(composable.pipelines.value).toHaveLength(1)
      expect(composable.pipelines.value[0].id).toBe('2')
      expect(composable.deletingPipeline.value).toBe(false)
    })

    it('should handle API errors and return false', async () => {
      const error = new Error('API Error')
      apiDeletePipeline.mockRejectedValue(error)

      const consoleSpy = vi.spyOn(console, 'error').mockImplementation(() => {})

      const result = await composable.deletePipeline('1')

      expect(consoleSpy).toHaveBeenCalledWith('Error deleting pipeline 1:', error)
      expect(result).toBe(false)
      expect(composable.error.value).toBe('API Error')
      expect(composable.deletingPipeline.value).toBe(false)

      consoleSpy.mockRestore()
    })

    it('should set deleting state correctly', async () => {
      apiDeletePipeline.mockImplementation(() => new Promise(resolve => {
        setTimeout(() => resolve(true), 100)
      }))

      const promise = composable.deletePipeline('1')

      expect(composable.deletingPipeline.value).toBe(true)

      await promise

      expect(composable.deletingPipeline.value).toBe(false)
    })
  })

  describe('executePipeline', () => {
    beforeEach(() => {
      composable.pipelines.value = [...mockPipelines]
    })

    it('should execute pipeline successfully', async () => {
      const executionResult = { executionId: 'exec-1', status: 'Running' }
      apiExecutePipeline.mockResolvedValue(executionResult)

      const result = await composable.executePipeline('1')

      expect(apiExecutePipeline).toHaveBeenCalledWith('1')
      expect(result).toEqual(executionResult)
      expect(composable.pipelines.value[0].status).toBe('Running')
      expect(composable.pipelines.value[0].lastRunAt).toBeDefined()
    })

    it('should handle API errors', async () => {
      const error = new Error('API Error')
      apiExecutePipeline.mockRejectedValue(error)

      const consoleSpy = vi.spyOn(console, 'error').mockImplementation(() => {})

      await expect(composable.executePipeline('1')).rejects.toThrow('API Error')

      expect(consoleSpy).toHaveBeenCalledWith('Error executing pipeline 1:', error)
      expect(composable.error.value).toBe('API Error')

      consoleSpy.mockRestore()
    })
  })

  describe('togglePipelineStatus', () => {
    beforeEach(() => {
      composable.pipelines.value = [...mockPipelines]
    })

    it('should toggle pipeline status successfully', async () => {
      const toggleResult = { id: '1', isActive: false }
      apiTogglePipelineStatus.mockResolvedValue(toggleResult)

      const result = await composable.togglePipelineStatus('1')

      expect(apiTogglePipelineStatus).toHaveBeenCalledWith('1')
      expect(result).toEqual(toggleResult)
      expect(composable.pipelines.value[0].isActive).toBe(false)
    })

    it('should handle API errors', async () => {
      const error = new Error('API Error')
      apiTogglePipelineStatus.mockRejectedValue(error)

      const consoleSpy = vi.spyOn(console, 'error').mockImplementation(() => {})

      await expect(composable.togglePipelineStatus('1')).rejects.toThrow('API Error')

      expect(consoleSpy).toHaveBeenCalledWith('Error toggling pipeline status 1:', error)
      expect(composable.error.value).toBe('API Error')

      consoleSpy.mockRestore()
    })
  })

  describe('createEmptyPipeline', () => {
    it('should create an empty pipeline object', () => {
      const emptyPipeline = composable.createEmptyPipeline()

      expect(emptyPipeline).toEqual({
        id: null,
        name: '',
        description: '',
        sourceId: null,
        destinationId: null,
        transformations: [],
        fieldMappings: [],
        isScheduled: false,
        isActive: true,
        schedule: {
          frequency: 'Daily',
          time: '00:00',
          cronExpression: '0 0 * * *'
        }
      })
    })
  })

  describe('getStatusColor', () => {
    it('should return correct colors for different statuses', () => {
      expect(composable.getStatusColor('completed')).toBe('success')
      expect(composable.getStatusColor('running')).toBe('info')
      expect(composable.getStatusColor('failed')).toBe('error')
      expect(composable.getStatusColor('idle')).toBe('grey')
      expect(composable.getStatusColor('unknown')).toBe('grey')
      expect(composable.getStatusColor(null)).toBe('grey')
    })
  })

  describe('formatDate', () => {
    it('should format date strings correctly', () => {
      const dateString = '2024-01-01T12:30:45Z'
      const result = composable.formatDate(dateString)

      expect(result).not.toBe('-')
      expect(result).toContain('1/1/2024') // Date format may vary by locale
    })

    it('should return dash for null/undefined dates', () => {
      expect(composable.formatDate(null)).toBe('-')
      expect(composable.formatDate(undefined)).toBe('-')
      expect(composable.formatDate('')).toBe('-')
    })
  })

  describe('setupTenantSubscription', () => {
    it('should setup tenant subscription without error', () => {
      // Just test that the method can be called without throwing
      expect(() => composable.setupTenantSubscription()).not.toThrow()
    })
  })

  describe('validatePipeline', () => {
    it('should validate pipeline successfully', async () => {
      const pipeline = {
        sourceId: 'source-1',
        destinationId: 'dest-1',
        transformations: [],
        isScheduled: false
      }

      const result = await composable.validatePipeline(pipeline)

      expect(result.isValid).toBe(true)
      expect(result.errors).toEqual([])
    })

    it('should return errors for missing source/destination', async () => {
      const pipeline = {
        transformations: [],
        isScheduled: false
      }

      const result = await composable.validatePipeline(pipeline)

      expect(result.isValid).toBe(false)
      expect(result.errors).toContain('Source is required')
      expect(result.errors).toContain('Destination is required')
    })
  })

  describe('getDependencies', () => {
    it('should calculate pipeline dependencies', () => {
      const pipelines = [
        { id: '1', name: 'Pipeline 1', sourceId: 'source-1', destinationId: 'dest-1' },
        { id: '2', name: 'Pipeline 2', sourceId: 'dest-1', destinationId: 'dest-2' },
        { id: '3', name: 'Pipeline 3', sourceId: 'source-2', destinationId: 'dest-3' }
      ]

      const dependencies = composable.getDependencies(pipelines)

      expect(dependencies.get('1')).toEqual([])
      expect(dependencies.get('2')).toEqual([
        { id: '1', name: 'Pipeline 1', type: 'source' }
      ])
      expect(dependencies.get('3')).toEqual([])
    })
  })

  describe('getExecutionOrder', () => {
    it('should calculate optimal execution order', () => {
      const pipelines = [
        { id: '1', name: 'Pipeline 1', sourceId: 'source-1', destinationId: 'dest-1' },
        { id: '2', name: 'Pipeline 2', sourceId: 'dest-1', destinationId: 'dest-2' },
        { id: '3', name: 'Pipeline 3', sourceId: 'source-2', destinationId: 'dest-3' }
      ]

      const order = composable.getExecutionOrder(pipelines)

      // Pipeline 1 should come before Pipeline 2 due to dependency
      expect(order.indexOf('1')).toBeLessThan(order.indexOf('2'))
    })
  })

  describe('errorStrategies', () => {
    it('should expose error handling strategies', () => {
      expect(composable.errorStrategies).toEqual({
        STOP: 'stop',
        CONTINUE: 'continue',
        RETRY: 'retry',
        SKIP: 'skip'
      })
    })
  })
})