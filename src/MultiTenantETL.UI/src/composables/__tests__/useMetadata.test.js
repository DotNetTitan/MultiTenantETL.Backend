import { describe, it, expect, beforeEach, vi } from 'vitest'
import { useMetadata, initializeMetadata } from '@/composables/useMetadata'
import { getAllMetadata, getCachedMetadata, cacheMetadata } from '@/services/metadataService'

// Mock the metadata service
vi.mock('@/services/metadataService', () => ({
  getAllMetadata: vi.fn(),
  getConnectorConfig: vi.fn(),
  getTransformationTypes: vi.fn(),
  cacheMetadata: vi.fn(),
  getCachedMetadata: vi.fn()
}))

describe('useMetadata Composable', () => {
  let composable

  const mockMetadata = {
    connectorConfig: {
      types: ['Database', 'API', 'File'],
      providers: {
        Database: ['PostgreSQL', 'MySQL', 'MongoDB'],
        API: ['REST', 'GraphQL'],
        File: ['CSV', 'JSON', 'XML']
      },
      directions: ['Inbound', 'Outbound', 'Bidirectional'],
      authTypes: ['None', 'Basic', 'Bearer', 'OAuth2'],
      fileFormats: ['CSV', 'JSON', 'XML', 'Parquet'],
      writeOperations: [
        { name: 'Insert', requiresPrimaryKey: false },
        { name: 'Update', requiresPrimaryKey: true },
        { name: 'Upsert', requiresPrimaryKey: true }
      ],
      httpMethods: ['GET', 'POST', 'PUT', 'DELETE']
    },
    transformationTypes: [
      { name: 'Filter', category: 'Data Quality', type: 'filter' },
      { name: 'Sort', category: 'Data Quality', type: 'sort' },
      { name: 'Join', category: 'Data Integration', type: 'join' },
      { name: 'Aggregate', category: 'Analytics', type: 'aggregate' }
    ],
    dataTypes: ['String', 'Number', 'Boolean', 'Date'],
    scheduleFrequencies: ['Daily', 'Weekly', 'Monthly'],
    daysOfWeek: ['Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday', 'Sunday']
  }

  beforeEach(() => {
    vi.clearAllMocks()
    composable = useMetadata()
    // Reset global state
    composable.metadata.value = null
    composable.loading.value = false
    composable.error.value = null
    composable.isInitialized.value = false
  })

  describe('Initial State', () => {
    it('should initialize with correct default values', () => {
      expect(composable.metadata.value).toBe(null)
      expect(composable.loading.value).toBe(false)
      expect(composable.error.value).toBe(null)
      expect(composable.isInitialized.value).toBe(false)
    })

    it('should have all computed properties return empty arrays when no metadata', () => {
      expect(composable.connectorTypes.value).toEqual([])
      expect(composable.connectorProviders.value).toEqual({})
      expect(composable.directions.value).toEqual([])
      expect(composable.authTypes.value).toEqual([])
      expect(composable.fileFormats.value).toEqual([])
      expect(composable.writeOperations.value).toEqual([])
      expect(composable.httpMethods.value).toEqual([])
      expect(composable.transformationTypes.value).toEqual([])
      expect(composable.dataTypes.value).toEqual([])
      expect(composable.scheduleFrequencies.value).toEqual([])
      expect(composable.daysOfWeek.value).toEqual([])
    })
  })

  describe('loadMetadata', () => {
    it('should load metadata from cache when available and not forcing refresh', async () => {
      getCachedMetadata.mockReturnValue(mockMetadata)

      await composable.loadMetadata()

      expect(getCachedMetadata).toHaveBeenCalled()
      expect(getAllMetadata).not.toHaveBeenCalled()
      expect(composable.metadata.value).toEqual(mockMetadata)
      expect(composable.isInitialized.value).toBe(true)
      expect(composable.loading.value).toBe(false)
    })

    it('should load metadata from API when cache is empty', async () => {
      getCachedMetadata.mockReturnValue(null)
      getAllMetadata.mockResolvedValue(mockMetadata)

      await composable.loadMetadata()

      expect(getCachedMetadata).toHaveBeenCalled()
      expect(getAllMetadata).toHaveBeenCalled()
      expect(cacheMetadata).toHaveBeenCalledWith(mockMetadata)
      expect(composable.metadata.value).toEqual(mockMetadata)
      expect(composable.isInitialized.value).toBe(true)
      expect(composable.loading.value).toBe(false)
    })

    it('should force refresh from API when requested', async () => {
      getAllMetadata.mockResolvedValue(mockMetadata)

      await composable.loadMetadata(true)

      expect(getCachedMetadata).not.toHaveBeenCalled()
      expect(getAllMetadata).toHaveBeenCalled()
      expect(cacheMetadata).toHaveBeenCalledWith(mockMetadata)
      expect(composable.metadata.value).toEqual(mockMetadata)
    })

    it('should handle API errors and use cached data as fallback', async () => {
      const error = new Error('API Error')
      getCachedMetadata.mockReturnValueOnce(null) // First call returns null
      getAllMetadata.mockRejectedValue(error)
      getCachedMetadata.mockReturnValueOnce(mockMetadata) // Second call returns cached data

      const consoleSpy = vi.spyOn(console, 'error').mockImplementation(() => {})
      const consoleWarnSpy = vi.spyOn(console, 'warn').mockImplementation(() => {})

      await composable.loadMetadata()

      expect(consoleSpy).toHaveBeenCalledWith('Error loading metadata:', error)
      expect(consoleWarnSpy).toHaveBeenCalledWith('Using cached metadata due to API error')
      expect(composable.error.value).toBe('API Error')
      expect(composable.metadata.value).toEqual(mockMetadata)
      expect(composable.isInitialized.value).toBe(true)

      consoleSpy.mockRestore()
      consoleWarnSpy.mockRestore()
    })

    it('should handle API errors with no cached fallback', async () => {
      const error = new Error('API Error')
      getCachedMetadata.mockReturnValue(null)
      getAllMetadata.mockRejectedValue(error)

      const consoleSpy = vi.spyOn(console, 'error').mockImplementation(() => {})

      await composable.loadMetadata()

      expect(consoleSpy).toHaveBeenCalledWith('Error loading metadata:', error)
      expect(composable.error.value).toBe('API Error')
      expect(composable.metadata.value).toBe(null)
      expect(composable.isInitialized.value).toBe(false)

      consoleSpy.mockRestore()
    })

    it('should not reload if already initialized', async () => {
      composable.isInitialized.value = true
      composable.metadata.value = mockMetadata

      await composable.loadMetadata()

      expect(getCachedMetadata).not.toHaveBeenCalled()
      expect(getAllMetadata).not.toHaveBeenCalled()
    })

    it('should set loading state correctly', async () => {
      getCachedMetadata.mockReturnValue(null)
      getAllMetadata.mockResolvedValue(mockMetadata)

      const promise = composable.loadMetadata()

      expect(composable.loading.value).toBe(true)

      await promise

      expect(composable.loading.value).toBe(false)
    })
  })

  describe('Computed Properties', () => {
    beforeEach(() => {
      composable.metadata.value = mockMetadata
    })

    it('should return connector types', () => {
      expect(composable.connectorTypes.value).toEqual(['Database', 'API', 'File'])
    })

    it('should return connector providers', () => {
      expect(composable.connectorProviders.value).toEqual({
        Database: ['PostgreSQL', 'MySQL', 'MongoDB'],
        API: ['REST', 'GraphQL'],
        File: ['CSV', 'JSON', 'XML']
      })
    })

    it('should return directions', () => {
      expect(composable.directions.value).toEqual(['Inbound', 'Outbound', 'Bidirectional'])
    })

    it('should return auth types', () => {
      expect(composable.authTypes.value).toEqual(['None', 'Basic', 'Bearer', 'OAuth2'])
    })

    it('should return file formats', () => {
      expect(composable.fileFormats.value).toEqual(['CSV', 'JSON', 'XML', 'Parquet'])
    })

    it('should return write operations', () => {
      expect(composable.writeOperations.value).toEqual([
        { name: 'Insert', requiresPrimaryKey: false },
        { name: 'Update', requiresPrimaryKey: true },
        { name: 'Upsert', requiresPrimaryKey: true }
      ])
    })

    it('should return http methods', () => {
      expect(composable.httpMethods.value).toEqual(['GET', 'POST', 'PUT', 'DELETE'])
    })

    it('should return transformation types', () => {
      expect(composable.transformationTypes.value).toEqual([
        { name: 'Filter', category: 'Data Quality', type: 'filter' },
        { name: 'Sort', category: 'Data Quality', type: 'sort' },
        { name: 'Join', category: 'Data Integration', type: 'join' },
        { name: 'Aggregate', category: 'Analytics', type: 'aggregate' }
      ])
    })

    it('should return data types', () => {
      expect(composable.dataTypes.value).toEqual(['String', 'Number', 'Boolean', 'Date'])
    })

    it('should return schedule frequencies', () => {
      expect(composable.scheduleFrequencies.value).toEqual(['Daily', 'Weekly', 'Monthly'])
    })

    it('should return days of week', () => {
      expect(composable.daysOfWeek.value).toEqual(['Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday', 'Sunday'])
    })
  })

  describe('Utility Functions', () => {
    beforeEach(() => {
      composable.metadata.value = mockMetadata
    })

    describe('getProvidersForType', () => {
      it('should return providers for existing type', () => {
        expect(composable.getProvidersForType('Database')).toEqual(['PostgreSQL', 'MySQL', 'MongoDB'])
        expect(composable.getProvidersForType('API')).toEqual(['REST', 'GraphQL'])
      })

      it('should return empty array for non-existing type', () => {
        expect(composable.getProvidersForType('Unknown')).toEqual([])
      })
    })

    describe('getWriteOperationsRequiringKey', () => {
      it('should return write operations that require primary key', () => {
        expect(composable.getWriteOperationsRequiringKey()).toEqual([
          { name: 'Update', requiresPrimaryKey: true },
          { name: 'Upsert', requiresPrimaryKey: true }
        ])
      })
    })

    describe('getTransformationsByCategory', () => {
      it('should return transformations for existing category', () => {
        expect(composable.getTransformationsByCategory('Data Quality')).toEqual([
          { name: 'Filter', category: 'Data Quality', type: 'filter' },
          { name: 'Sort', category: 'Data Quality', type: 'sort' }
        ])
      })

      it('should return empty array for category with no transformations', () => {
        expect(composable.getTransformationsByCategory('Unknown')).toEqual([])
      })
    })

    describe('getTransformationCategories', () => {
      it('should return unique transformation categories', () => {
        expect(composable.getTransformationCategories()).toEqual([
          'Data Quality',
          'Data Integration',
          'Analytics'
        ])
      })
    })
  })

  describe('initializeMetadata', () => {
    it('should call loadMetadata', async () => {
      getCachedMetadata.mockReturnValue(mockMetadata)

      await initializeMetadata()

      expect(getCachedMetadata).toHaveBeenCalled()
    })
  })
})