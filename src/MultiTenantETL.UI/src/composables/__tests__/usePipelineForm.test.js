import { describe, it, expect, beforeEach, vi } from 'vitest'
import { ref } from 'vue'
import { usePipelineForm } from '@/composables/usePipelineForm'
import { fetchConnectors } from '@/services/connectorService'

// Mock dependencies
vi.mock('@/composables/usePipeline', () => ({
  usePipeline: () => ({
    createEmptyPipeline: vi.fn(() => ({
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
    }))
  })
}))

vi.mock('@/services/connectorService', () => ({
  fetchConnectors: vi.fn()
}))

describe('usePipelineForm Composable', () => {
  let composable
  let mockUsePipeline

  const mockConnectors = [
    {
      id: '1',
      name: 'PostgreSQL Source',
      type: 'Database',
      provider: 'PostgreSQL',
      direction: 'source'
    },
    {
      id: '2',
      name: 'MySQL Destination',
      type: 'Database',
      provider: 'MySQL',
      direction: 'destination'
    },
    {
      id: '3',
      name: 'CSV File Source',
      type: 'File',
      provider: 'CSV',
      direction: 'source'
    }
  ]

  const mockPipeline = {
    id: 'pipeline-1',
    name: 'Test Pipeline',
    description: 'A test pipeline',
    sourceId: '1',
    sourceName: 'PostgreSQL Source',
    destinationId: '2',
    destinationName: 'MySQL Destination',
    transformations: [
      { id: 't1', name: 'Filter', type: 'filter' }
    ],
    fieldMappings: [
      { source: 'field1', target: 'column1' }
    ],
    isScheduled: true,
    isActive: true,
    schedule: {
      frequency: 'Daily',
      time: '09:00',
      cronExpression: '0 9 * * *',
      timezone: 'America/New_York'
    }
  }

  beforeEach(() => {
    vi.clearAllMocks()

    // Get the mocked composable
    composable = usePipelineForm()
    mockUsePipeline = {
      createEmptyPipeline: vi.fn(() => ({
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
      }))
    }
  })

  describe('Initial State', () => {
    it('should initialize with correct default values', () => {
      expect(composable.connectors.value).toEqual([])
      expect(composable.form.value).toBe(null)
      expect(composable.timezones.value).toEqual([
        { value: 'UTC', name: 'UTC (Coordinated Universal Time)' },
        { value: 'America/New_York', name: 'Eastern Time (US & Canada)' },
        { value: 'America/Chicago', name: 'Central Time (US & Canada)' },
        { value: 'America/Denver', name: 'Mountain Time (US & Canada)' },
        { value: 'America/Los_Angeles', name: 'Pacific Time (US & Canada)' },
        { value: 'Europe/London', name: 'London (GMT)' },
        { value: 'Europe/Paris', name: 'Paris (Central European Time)' },
        { value: 'Asia/Tokyo', name: 'Tokyo (Japan Standard Time)' },
        { value: 'Asia/Shanghai', name: 'China Standard Time' },
        { value: 'Australia/Sydney', name: 'Sydney (Australian Eastern Time)' }
      ])
    })

    it('should initialize editedPipeline with empty pipeline', () => {
      expect(composable.editedPipeline.value.id).toBe(null)
      expect(composable.editedPipeline.value.name).toBe('')
      expect(composable.editedPipeline.value.sourceId).toBe(null)
      expect(composable.editedPipeline.value.destinationId).toBe(null)
      expect(composable.editedPipeline.value.transformations).toEqual([])
      expect(composable.editedPipeline.value.fieldMappings).toEqual([])
      expect(composable.editedPipeline.value.isScheduled).toBe(false)
      expect(composable.editedPipeline.value.isActive).toBe(true)
    })
  })

  describe('fetchConnectors', () => {
    it('should fetch connectors successfully', async () => {
      fetchConnectors.mockResolvedValue(mockConnectors)

      await composable.fetchConnectors()

      expect(fetchConnectors).toHaveBeenCalledWith()
      expect(composable.connectors.value).toEqual(mockConnectors)
    })

    it('should handle API errors', async () => {
      const error = new Error('API Error')
      fetchConnectors.mockRejectedValue(error)

      const consoleSpy = vi.spyOn(console, 'error').mockImplementation(() => {})

      await composable.fetchConnectors()

      expect(consoleSpy).toHaveBeenCalledWith('Error fetching connectors:', error)

      consoleSpy.mockRestore()
    })
  })

  describe('prepareEditPipeline', () => {
    beforeEach(() => {
      composable.connectors.value = mockConnectors
    })

    it('should prepare pipeline for editing with connector objects', () => {
      composable.prepareEditPipeline(mockPipeline)

      expect(composable.editedPipeline.value.id).toBe('pipeline-1')
      expect(composable.editedPipeline.value.name).toBe('Test Pipeline')
      expect(composable.editedPipeline.value.description).toBe('A test pipeline')
      expect(composable.editedPipeline.value.sourceId).toEqual(mockConnectors[0]) // PostgreSQL Source
      expect(composable.editedPipeline.value.destinationId).toEqual(mockConnectors[1]) // MySQL Destination
      expect(composable.editedPipeline.value.transformations).toEqual([
        { id: 't1', name: 'Filter', type: 'filter' }
      ])
      expect(composable.editedPipeline.value.fieldMappings).toEqual([
        { source: 'field1', target: 'column1' }
      ])
      expect(composable.editedPipeline.value.isScheduled).toBe(true)
      expect(composable.editedPipeline.value.isActive).toBe(true)
      expect(composable.editedPipeline.value.schedule).toEqual({
        frequency: 'Daily',
        time: '09:00',
        cronExpression: '0 9 * * *',
        timezone: 'America/New_York'
      })
    })

    it('should handle pipeline with missing connectors', () => {
      const pipelineWithMissingConnectors = {
        ...mockPipeline,
        sourceId: '999', // Non-existent ID
        sourceName: 'Non-existent Source', // Non-existent name
        destinationId: '888', // Non-existent ID
        destinationName: 'Non-existent Destination' // Non-existent name
      }

      composable.prepareEditPipeline(pipelineWithMissingConnectors)

      expect(composable.editedPipeline.value.sourceId).toBe(null)
      expect(composable.editedPipeline.value.destinationId).toBe(null)
    })

    it('should handle pipeline without schedule', () => {
      const pipelineWithoutSchedule = {
        ...mockPipeline,
        schedule: undefined
      }

      composable.prepareEditPipeline(pipelineWithoutSchedule)

      expect(composable.editedPipeline.value.schedule).toEqual({
        frequency: 'Daily',
        time: '00:00',
        cronExpression: '0 0 * * *',
        timezone: 'UTC'
      })
    })

    it('should handle pipeline with schedule missing timezone', () => {
      const pipelineWithoutTimezone = {
        ...mockPipeline,
        schedule: {
          frequency: 'Weekly',
          time: '15:30',
          cronExpression: '30 15 * * 1'
        }
      }

      composable.prepareEditPipeline(pipelineWithoutTimezone)

      expect(composable.editedPipeline.value.schedule.timezone).toBe('UTC')
    })

    it('should deep clone transformations and field mappings', () => {
      composable.prepareEditPipeline(mockPipeline)

      // Modify the prepared pipeline
      composable.editedPipeline.value.transformations[0].name = 'Modified Filter'
      composable.editedPipeline.value.fieldMappings[0].source = 'modifiedField'

      // Original should remain unchanged
      expect(mockPipeline.transformations[0].name).toBe('Filter')
      expect(mockPipeline.fieldMappings[0].source).toBe('field1')
    })
  })

  describe('resetForm', () => {
    beforeEach(() => {
      composable.connectors.value = mockConnectors
      composable.editedPipeline.value = { ...mockPipeline }
    })

    it('should reset editedPipeline to empty state', () => {
      composable.resetForm()

      expect(composable.editedPipeline.value.id).toBe(null)
      expect(composable.editedPipeline.value.name).toBe('')
      expect(composable.editedPipeline.value.sourceId).toBe(null)
      expect(composable.editedPipeline.value.destinationId).toBe(null)
    })

    it('should reset form validation if form ref exists', () => {
      const mockForm = {
        resetValidation: vi.fn()
      }
      composable.form.value = mockForm

      composable.resetForm()

      expect(mockForm.resetValidation).toHaveBeenCalled()
    })

    it('should handle missing form ref gracefully', () => {
      composable.form.value = null

      expect(() => composable.resetForm()).not.toThrow()
    })
  })

  describe('timezones', () => {
    it('should expose the complete timezones list', () => {
      expect(composable.timezones.value).toHaveLength(10)
      expect(composable.timezones.value[0]).toEqual({
        value: 'UTC',
        name: 'UTC (Coordinated Universal Time)'
      })
      expect(composable.timezones.value[1]).toEqual({
        value: 'America/New_York',
        name: 'Eastern Time (US & Canada)'
      })
    })
  })
})