import { describe, it, expect, beforeEach, vi } from 'vitest'
import { useTranslatedMetadata } from '@/composables/useTranslatedMetadata'
import { useMetadata } from '@/composables/useMetadata'

// Mock vue-i18n
vi.mock('vue-i18n', () => ({
  useI18n: () => ({
    t: vi.fn((key) => `translated_${key}`)
  })
}))

// Mock useMetadata
vi.mock('@/composables/useMetadata', () => ({
  useMetadata: vi.fn()
}))

describe('useTranslatedMetadata Composable', () => {
  let composable
  let mockMetadata
  let mockT

  const mockMetadataData = {
    connectorTypes: [
      { id: 'database', labelKey: 'metadata.connector.database', descriptionKey: 'metadata.connector.database.desc' },
      { id: 'api', labelKey: 'metadata.connector.api', categoryKey: 'metadata.category.connectors' }
    ],
    directions: [
      { id: 'source', labelKey: 'metadata.direction.source' },
      { id: 'destination', labelKey: 'metadata.direction.destination' }
    ],
    authTypes: [
      { id: 'basic', labelKey: 'metadata.auth.basic' }
    ],
    fileFormats: [
      { id: 'csv', labelKey: 'metadata.format.csv' }
    ],
    writeOperations: [
      { id: 'insert', labelKey: 'metadata.write.insert', requiresPrimaryKey: false },
      { id: 'update', labelKey: 'metadata.write.update', requiresPrimaryKey: true }
    ],
    httpMethods: [
      { id: 'GET', labelKey: 'metadata.http.get' }
    ],
    transformationTypes: [
      { id: 'filter', labelKey: 'metadata.transform.filter', categoryKey: 'metadata.category.data_quality' },
      { id: 'map', labelKey: 'metadata.transform.map', categoryKey: 'metadata.category.data_quality' },
      { id: 'aggregate', labelKey: 'metadata.transform.aggregate', categoryKey: 'metadata.category.aggregation' }
    ],
    dataTypes: [
      { id: 'string', labelKey: 'metadata.datatype.string' }
    ],
    scheduleFrequencies: [
      { id: 'daily', labelKey: 'metadata.schedule.daily' }
    ],
    daysOfWeek: [
      { id: 'monday', labelKey: 'metadata.day.monday', shortKey: 'metadata.day.mon' }
    ],
    connectorProviders: {
      database: ['PostgreSQL', 'MySQL'],
      api: ['REST']
    }
  }

  beforeEach(() => {
    vi.clearAllMocks()

    // Setup mock metadata
    mockMetadata = {
      loading: { value: false },
      error: { value: null },
      isInitialized: { value: true },
      loadMetadata: vi.fn(),
      getProvidersForType: vi.fn((type) => mockMetadataData.connectorProviders[type] || []),
      getWriteOperationsRequiringKey: vi.fn(() => [
        mockMetadataData.writeOperations[1] // update operation
      ]),
      connectorTypes: { value: mockMetadataData.connectorTypes },
      directions: { value: mockMetadataData.directions },
      authTypes: { value: mockMetadataData.authTypes },
      fileFormats: { value: mockMetadataData.fileFormats },
      writeOperations: { value: mockMetadataData.writeOperations },
      httpMethods: { value: mockMetadataData.httpMethods },
      transformationTypes: { value: mockMetadataData.transformationTypes },
      dataTypes: { value: mockMetadataData.dataTypes },
      scheduleFrequencies: { value: mockMetadataData.scheduleFrequencies },
      daysOfWeek: { value: mockMetadataData.daysOfWeek },
      connectorProviders: { value: mockMetadataData.connectorProviders }
    }

    useMetadata.mockReturnValue(mockMetadata)

    // Setup mock i18n
    mockT = vi.fn((key) => `translated_${key}`)

    composable = useTranslatedMetadata()
  })

  describe('Initialization', () => {
    it('should initialize with metadata state', () => {
      expect(composable.loading).toBe(mockMetadata.loading)
      expect(composable.error).toBe(mockMetadata.error)
      expect(composable.isInitialized).toBe(mockMetadata.isInitialized)
      expect(composable.loadMetadata).toBe(mockMetadata.loadMetadata)
    })
  })

  describe('translateItem', () => {
    it('should translate labelKey to label', () => {
      const item = { id: 'test', labelKey: 'test.key' }
      const result = composable.translateItem(item)

      expect(result).toEqual({
        id: 'test',
        labelKey: 'test.key',
        label: 'translated_test.key'
      })
    })

    it('should translate descriptionKey to description', () => {
      const item = { id: 'test', descriptionKey: 'test.desc' }
      const result = composable.translateItem(item)

      expect(result.description).toBe('translated_test.desc')
    })

    it('should translate categoryKey to category', () => {
      const item = { id: 'test', categoryKey: 'test.category' }
      const result = composable.translateItem(item)

      expect(result.category).toBe('translated_test.category')
    })

    it('should translate shortKey to short', () => {
      const item = { id: 'test', shortKey: 'test.short' }
      const result = composable.translateItem(item)

      expect(result.short).toBe('translated_test.short')
    })

    it('should handle null/undefined items', () => {
      expect(composable.translateItem(null)).toBe(null)
      expect(composable.translateItem(undefined)).toBe(undefined)
    })

    it('should return item unchanged if no translation keys', () => {
      const item = { id: 'test', name: 'Test' }
      const result = composable.translateItem(item)

      expect(result).toEqual(item)
    })

    it('should preserve original properties', () => {
      const item = {
        id: 'test',
        labelKey: 'test.key',
        customProp: 'value',
        nested: { prop: 'value' }
      }
      const result = composable.translateItem(item)

      expect(result.id).toBe('test')
      expect(result.customProp).toBe('value')
      expect(result.nested).toEqual({ prop: 'value' })
      expect(result.label).toBe('translated_test.key')
    })
  })

  describe('translateItems', () => {
    it('should translate array of items', () => {
      const items = [
        { id: '1', labelKey: 'key1' },
        { id: '2', labelKey: 'key2' }
      ]
      const result = composable.translateItems(items)

      expect(result).toHaveLength(2)
      expect(result[0].label).toBe('translated_key1')
      expect(result[1].label).toBe('translated_key2')
    })

    it('should handle null/undefined arrays', () => {
      expect(composable.translateItems(null)).toEqual([])
      expect(composable.translateItems(undefined)).toEqual([])
    })

    it('should handle non-array inputs', () => {
      expect(composable.translateItems('not array')).toEqual([])
      expect(composable.translateItems(123)).toEqual([])
    })

    it('should handle empty arrays', () => {
      expect(composable.translateItems([])).toEqual([])
    })
  })

  describe('Computed Properties', () => {
    it('should translate connectorTypes', () => {
      const result = composable.connectorTypes.value

      expect(result).toHaveLength(2)
      expect(result[0].label).toBe('translated_metadata.connector.database')
      expect(result[0].description).toBe('translated_metadata.connector.database.desc')
      expect(result[1].category).toBe('translated_metadata.category.connectors')
    })

    it('should translate directions', () => {
      const result = composable.directions.value

      expect(result).toHaveLength(2)
      expect(result[0].label).toBe('translated_metadata.direction.source')
      expect(result[1].label).toBe('translated_metadata.direction.destination')
    })

    it('should translate authTypes', () => {
      const result = composable.authTypes.value

      expect(result).toHaveLength(1)
      expect(result[0].label).toBe('translated_metadata.auth.basic')
    })

    it('should translate fileFormats', () => {
      const result = composable.fileFormats.value

      expect(result).toHaveLength(1)
      expect(result[0].label).toBe('translated_metadata.format.csv')
    })

    it('should translate writeOperations', () => {
      const result = composable.writeOperations.value

      expect(result).toHaveLength(2)
      expect(result[0].label).toBe('translated_metadata.write.insert')
      expect(result[1].label).toBe('translated_metadata.write.update')
    })

    it('should translate httpMethods', () => {
      const result = composable.httpMethods.value

      expect(result).toHaveLength(1)
      expect(result[0].label).toBe('translated_metadata.http.get')
    })

    it('should translate transformationTypes', () => {
      const result = composable.transformationTypes.value

      expect(result).toHaveLength(3)
      expect(result[0].label).toBe('translated_metadata.transform.filter')
      expect(result[1].label).toBe('translated_metadata.transform.map')
      expect(result[2].label).toBe('translated_metadata.transform.aggregate')
    })

    it('should translate dataTypes', () => {
      const result = composable.dataTypes.value

      expect(result).toHaveLength(1)
      expect(result[0].label).toBe('translated_metadata.datatype.string')
    })

    it('should translate scheduleFrequencies', () => {
      const result = composable.scheduleFrequencies.value

      expect(result).toHaveLength(1)
      expect(result[0].label).toBe('translated_metadata.schedule.daily')
    })

    it('should translate daysOfWeek with short translations', () => {
      const result = composable.daysOfWeek.value

      expect(result).toHaveLength(1)
      expect(result[0].label).toBe('translated_metadata.day.monday')
      expect(result[0].short).toBe('translated_metadata.day.mon')
    })

    it('should not translate connectorProviders', () => {
      expect(composable.connectorProviders).toBe(mockMetadata.connectorProviders)
    })
  })

  describe('getProvidersForType', () => {
    it('should delegate to metadata.getProvidersForType', () => {
      const result = composable.getProvidersForType('database')

      expect(mockMetadata.getProvidersForType).toHaveBeenCalledWith('database')
      expect(result).toEqual(['PostgreSQL', 'MySQL'])
    })
  })

  describe('getWriteOperationsRequiringKey', () => {
    it('should return translated write operations requiring primary key', () => {
      const result = composable.getWriteOperationsRequiringKey()

      expect(mockMetadata.getWriteOperationsRequiringKey).toHaveBeenCalled()
      expect(result).toHaveLength(1)
      expect(result[0].label).toBe('translated_metadata.write.update')
    })
  })

  describe('getTransformationsByCategory', () => {
    it('should return translated transformations for a category', () => {
      const result = composable.getTransformationsByCategory('metadata.category.data_quality')

      expect(result).toHaveLength(2)
      expect(result[0].label).toBe('translated_metadata.transform.filter')
      expect(result[1].label).toBe('translated_metadata.transform.map')
    })

    it('should return empty array for non-existent category', () => {
      const result = composable.getTransformationsByCategory('nonexistent')

      expect(result).toEqual([])
    })
  })

  describe('getTransformationCategories', () => {
    it('should return unique translated category names', () => {
      const result = composable.getTransformationCategories()

      expect(result).toHaveLength(2)
      expect(result).toContain('translated_metadata.category.data_quality')
      expect(result).toContain('translated_metadata.category.aggregation')
    })
  })

  describe('getTranslatedOptions', () => {
    it('should return translated options for valid type', () => {
      const result = composable.getTranslatedOptions('connectorTypes')

      expect(result).toHaveLength(2)
      expect(result[0].label).toBe('translated_metadata.connector.database')
    })

    it('should return empty array for invalid type', () => {
      const result = composable.getTranslatedOptions('invalidType')

      expect(result).toEqual([])
    })

    it('should support all metadata types', () => {
      const types = [
        'connectorTypes', 'directions', 'authTypes', 'fileFormats',
        'writeOperations', 'httpMethods', 'transformationTypes',
        'dataTypes', 'scheduleFrequencies', 'daysOfWeek'
      ]

      types.forEach(type => {
        const result = composable.getTranslatedOptions(type)
        expect(Array.isArray(result)).toBe(true)
      })
    })
  })
})