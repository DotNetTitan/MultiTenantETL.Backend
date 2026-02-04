import { describe, it, expect, beforeEach, vi } from 'vitest'
import { useProviderMetadata } from '@/composables/useProviderMetadata'
import { getConnectorConfig } from '@/services/metadataService'

// Mock metadata service
vi.mock('@/services/metadataService', () => ({
  getConnectorConfig: vi.fn()
}))

// Import the composable to access module-level refs for resetting
import * as providerMetadataModule from '@/composables/useProviderMetadata'

describe('useProviderMetadata Composable', () => {
  let composable

  const mockProviderMetadata = {
    PostgreSQL: {
      icon: 'mdi-database',
      color: 'blue'
    },
    MySQL: {
      icon: 'mdi-database-outline',
      color: 'orange'
    },
    S3: {
      icon: 'mdi-aws',
      color: 'green'
    },
    REST: {
      icon: 'mdi-api',
      color: 'purple'
    }
  }

  const mockConnectorConfig = {
    types: ['Database', 'API', 'File'],
    providers: {
      Database: ['PostgreSQL', 'MySQL'],
      API: ['REST'],
      File: ['S3']
    },
    providerMetadata: mockProviderMetadata
  }

  beforeEach(() => {
    vi.clearAllMocks()
    // Create a fresh composable instance for each test
    composable = useProviderMetadata()
    // Reset module-level state for clean test isolation
    composable._resetState()
  })

  describe('Initial State', () => {
    it('should initialize with null provider metadata', () => {
      expect(composable.providerMetadata.value).toBe(null)
      expect(composable.loading.value).toBe(false)
      expect(composable.error.value).toBe(null)
      expect(composable.isLoaded.value).toBe(false)
    })
  })

  describe('loadProviderMetadata', () => {
    it('should load provider metadata successfully', async () => {
      getConnectorConfig.mockResolvedValue(mockConnectorConfig)

      const result = await composable.loadProviderMetadata()

      expect(getConnectorConfig).toHaveBeenCalled()
      expect(result).toEqual(mockProviderMetadata)
      expect(composable.providerMetadata.value).toEqual(mockProviderMetadata)
      expect(composable.loading.value).toBe(false)
      expect(composable.error.value).toBe(null)
      expect(composable.isLoaded.value).toBe(true)
    })

    it('should return cached metadata on subsequent calls', async () => {
      getConnectorConfig.mockResolvedValue(mockConnectorConfig)

      // First call
      await composable.loadProviderMetadata()
      // Second call should return cached data without calling API
      getConnectorConfig.mockClear() // Clear call count
      const result = await composable.loadProviderMetadata()

      expect(getConnectorConfig).not.toHaveBeenCalled() // Should not call API again
      expect(result).toEqual(mockProviderMetadata)
    })

    it('should handle API errors', async () => {
      // Reset mock for this test
      getConnectorConfig.mockReset()
      const error = new Error('API Error')
      getConnectorConfig.mockRejectedValue(error)

      // Reset state for clean test
      composable._resetState()

      await expect(composable.loadProviderMetadata()).rejects.toThrow('API Error')

      expect(composable.error.value).toBe(error)
      expect(composable.loading.value).toBe(false)
      expect(composable.providerMetadata.value).toBe(null)
      expect(composable.isLoaded.value).toBe(false)
    })

    it('should handle missing providerMetadata in response', async () => {
      // Reset mock for this test
      getConnectorConfig.mockReset()
      const configWithoutMetadata = {
        types: ['Database'],
        providers: { Database: ['PostgreSQL'] }
        // No providerMetadata
      }
      getConnectorConfig.mockResolvedValue(configWithoutMetadata)

      // Reset state for clean test
      composable._resetState()

      await expect(composable.loadProviderMetadata()).rejects.toThrow('Provider metadata not found in backend response')

      expect(composable.error.value.message).toBe('Provider metadata not found in backend response')
      expect(composable.loading.value).toBe(false)
    })

    it('should set loading state correctly', async () => {
      // Reset mock for this test
      getConnectorConfig.mockReset()
      getConnectorConfig.mockImplementation(() => new Promise(resolve => {
        setTimeout(() => resolve(mockConnectorConfig), 10)
      }))

      // Reset state for clean test
      composable._resetState()

      const promise = composable.loadProviderMetadata()

      expect(composable.loading.value).toBe(true)

      await promise

      expect(composable.loading.value).toBe(false)
    })
  })

  describe('getProviderIcon', () => {
    it('should return correct icon for known provider', async () => {
      getConnectorConfig.mockResolvedValue(mockConnectorConfig)
      await composable.loadProviderMetadata()

      expect(composable.getProviderIcon('PostgreSQL')).toBe('mdi-database')
      expect(composable.getProviderIcon('MySQL')).toBe('mdi-database-outline')
      expect(composable.getProviderIcon('S3')).toBe('mdi-aws')
    })

    it('should return default icon for unknown provider', async () => {
      getConnectorConfig.mockResolvedValue(mockConnectorConfig)
      await composable.loadProviderMetadata()

      const consoleSpy = vi.spyOn(console, 'warn').mockImplementation(() => {})

      const result = composable.getProviderIcon('UnknownProvider')

      expect(result).toBe('mdi-connection')
      expect(consoleSpy).toHaveBeenCalledWith('No icon found for provider: UnknownProvider')

      consoleSpy.mockRestore()
    })

    it('should return default icon for null/undefined provider', async () => {
      getConnectorConfig.mockResolvedValue(mockConnectorConfig)
      await composable.loadProviderMetadata()

      expect(composable.getProviderIcon(null)).toBe('mdi-connection')
      expect(composable.getProviderIcon(undefined)).toBe('mdi-connection')
      expect(composable.getProviderIcon('')).toBe('mdi-connection')
    })

    it('should match provider case-insensitively', async () => {
      getConnectorConfig.mockResolvedValue(mockConnectorConfig)
      await composable.loadProviderMetadata()

      expect(composable.getProviderIcon('postgresql')).toBe('mdi-database')
      expect(composable.getProviderIcon('MYSQL')).toBe('mdi-database-outline')
    })

    it('should return default icon when metadata not loaded', () => {
      // Reset state for clean test - ensure no metadata is loaded
      composable._resetState()

      expect(composable.getProviderIcon('PostgreSQL')).toBe('mdi-connection')
    })
  })

  describe('getProviderColor', () => {
    it('should return correct color for known provider', async () => {
      getConnectorConfig.mockResolvedValue(mockConnectorConfig)
      await composable.loadProviderMetadata()

      expect(composable.getProviderColor('PostgreSQL')).toBe('blue')
      expect(composable.getProviderColor('MySQL')).toBe('orange')
      expect(composable.getProviderColor('REST')).toBe('purple')
    })

    it('should return default color for unknown provider', async () => {
      getConnectorConfig.mockResolvedValue(mockConnectorConfig)
      await composable.loadProviderMetadata()

      const consoleSpy = vi.spyOn(console, 'warn').mockImplementation(() => {})

      const result = composable.getProviderColor('UnknownProvider')

      expect(result).toBe('grey')
      expect(consoleSpy).toHaveBeenCalledWith('No color found for provider: UnknownProvider')

      consoleSpy.mockRestore()
    })

    it('should return default color for null/undefined provider', async () => {
      getConnectorConfig.mockResolvedValue(mockConnectorConfig)
      await composable.loadProviderMetadata()

      expect(composable.getProviderColor(null)).toBe('grey')
      expect(composable.getProviderColor(undefined)).toBe('grey')
      expect(composable.getProviderColor('')).toBe('grey')
    })

    it('should match provider case-insensitively', async () => {
      getConnectorConfig.mockResolvedValue(mockConnectorConfig)
      await composable.loadProviderMetadata()

      expect(composable.getProviderColor('postgresql')).toBe('blue')
      expect(composable.getProviderColor('rest')).toBe('purple')
    })

    it('should return default color when metadata not loaded', () => {
      // Reset state for clean test - ensure no metadata is loaded
      composable._resetState()

      expect(composable.getProviderColor('PostgreSQL')).toBe('grey')
    })
  })

  describe('isLoaded computed property', () => {
    it('should return false when metadata is null', () => {
      // Reset state for clean test - ensure no metadata is loaded
      composable._resetState()
      expect(composable.isLoaded.value).toBe(false)
    })

    it('should return true when metadata is loaded', async () => {
      getConnectorConfig.mockResolvedValue(mockConnectorConfig)
      await composable.loadProviderMetadata()

      expect(composable.isLoaded.value).toBe(true)
    })
  })
})