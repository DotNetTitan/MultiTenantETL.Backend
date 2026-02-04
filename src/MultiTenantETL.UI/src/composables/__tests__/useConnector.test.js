import { describe, it, expect, beforeEach, vi } from 'vitest'
import { useConnector } from '@/composables/useConnector'
import { connectorService } from '@/services/connectorService'

// Mock the connector service
vi.mock('@/services/connectorService', () => {
  const testConnection = vi.fn()
  const testExisting = vi.fn()
  const detectSchema = vi.fn()
  const detectSchemaPreview = vi.fn()

  return {
    connectorService: {
      testConnection,
      testExisting,
      detectSchema,
      detectSchemaPreview
    }
  }
})

describe('useConnector Composable', () => {
  let composable

  beforeEach(() => {
    vi.clearAllMocks()
    composable = useConnector()

    // Reset mocks
    connectorService.testConnection.mockReset()
    connectorService.testExisting.mockReset()
    connectorService.detectSchema.mockReset()
    connectorService.detectSchemaPreview.mockReset()
  })

  describe('Initial State', () => {
    it('should initialize with correct default values', () => {
      expect(composable.loading.value).toBe(false)
      expect(composable.error.value).toBe(null)
      expect(composable.schema.value).toBe(null)
    })
  })

  describe('validateConnection', () => {
    it('should successfully validate a new connection and set schema', async () => {
      const mockResponse = {
        success: true,
        message: 'Connection test successful',
        schema: {
          tables: [
            { name: 'users', columns: [{ name: 'id', type: 'int' }, { name: 'name', type: 'varchar' }] },
            { name: 'orders', columns: [{ name: 'id', type: 'int' }, { name: 'user_id', type: 'int' }] }
          ]
        },
        details: 'Connected successfully to database'
      }

      connectorService.testConnection.mockResolvedValue(mockResponse)

      const connector = {
        type: 'Database',
        provider: 'SqlServer',
        config: {
          server: 'localhost',
          database: 'test',
          username: 'user',
          password: 'pass'
        }
      }

      const result = await composable.validateConnection(connector)

      expect(connectorService.testConnection).toHaveBeenCalledWith(connector)
      expect(composable.loading.value).toBe(false)
      expect(composable.error.value).toBe(null)
      expect(result.success).toBe(true)
      expect(result.message).toBe('Connection test successful')
      expect(result.schema).toBeDefined()
      expect(result.schema.tables).toHaveLength(2)
      expect(composable.schema.value).toEqual(result.schema)
    })

    it('should handle connection validation errors', async () => {
      const mockError = new Error('Connection failed')
      mockError.userMessage = 'Connection failed'

      connectorService.testConnection.mockRejectedValue(mockError)

      const connector = {
        type: 'Database',
        provider: 'SqlServer',
        config: {}
      }

      const result = await composable.validateConnection(connector)

      expect(connectorService.testConnection).toHaveBeenCalledWith(connector)
      expect(composable.loading.value).toBe(false)
      expect(composable.error.value).toBe('Connection failed')
      expect(result.success).toBe(false)
      expect(result.message).toBe('Connection failed')
    })

    it('should set loading state during validation', async () => {
      connectorService.testConnection.mockResolvedValue({
        success: true,
        message: 'Success'
      })

      const connector = {
        type: 'Database',
        provider: 'SqlServer',
        config: {}
      }

      const promise = composable.validateConnection(connector)

      expect(composable.loading.value).toBe(true)

      await promise

      expect(composable.loading.value).toBe(false)
    })
  })

  describe('testExistingConnection', () => {
    it('should successfully test an existing connection', async () => {
      const mockResponse = {
        success: true,
        message: 'Connection test successful',
        details: 'Connected successfully'
      }

      connectorService.testExisting.mockResolvedValue(mockResponse)

      const connectorId = 'connector-1'

      const result = await composable.testExistingConnection(connectorId)

      expect(connectorService.testExisting).toHaveBeenCalledWith(connectorId)
      expect(composable.loading.value).toBe(false)
      expect(composable.error.value).toBe(null)
      expect(result.success).toBe(true)
      expect(result.message).toBe('Connection test successful')
    })

    it('should handle errors when testing existing connection', async () => {
      const mockError = new Error('Connection failed')
      mockError.userMessage = 'Connection failed'

      connectorService.testExisting.mockRejectedValue(mockError)

      const connectorId = 'non-existent'

      const result = await composable.testExistingConnection(connectorId)

      expect(connectorService.testExisting).toHaveBeenCalledWith(connectorId)
      expect(composable.loading.value).toBe(false)
      expect(composable.error.value).toBe('Connection failed')
      expect(result.success).toBe(false)
    })

    it('should set loading state during existing connection test', async () => {
      connectorService.testExisting.mockResolvedValue({
        success: true,
        message: 'Success'
      })

      const connectorId = 'connector-1'

      const promise = composable.testExistingConnection(connectorId)

      expect(composable.loading.value).toBe(true)

      await promise

      expect(composable.loading.value).toBe(false)
    })
  })

  describe('detectSchema', () => {
    it('should successfully detect schema for existing connector', async () => {
      const mockResponse = {
        success: true,
        message: 'Schema detected successfully',
        schema: {
          tables: [
            { name: 'users', columns: [{ name: 'id', type: 'int' }, { name: 'name', type: 'varchar' }] },
            { name: 'orders', columns: [{ name: 'id', type: 'int' }, { name: 'user_id', type: 'int' }] }
          ]
        }
      }

      connectorService.detectSchema.mockResolvedValue(mockResponse)

      const connectorId = 'connector-1'

      const result = await composable.detectSchema(connectorId)

      expect(connectorService.detectSchema).toHaveBeenCalledWith(connectorId, null)
      expect(composable.loading.value).toBe(false)
      expect(composable.error.value).toBe(null)
      expect(result).toBeDefined()
      expect(result.tables).toHaveLength(2)
      expect(composable.schema.value).toEqual(result)
    })

    it('should detect schema with specific table name', async () => {
      const mockResponse = {
        success: true,
        message: 'Schema detected successfully',
        schema: {
          tables: [
            { name: 'users', columns: [{ name: 'id', type: 'int' }, { name: 'name', type: 'varchar' }] }
          ]
        }
      }

      connectorService.detectSchema.mockResolvedValue(mockResponse)

      const connectorId = 'connector-1'
      const tableName = 'users'

      const result = await composable.detectSchema(connectorId, tableName)

      expect(connectorService.detectSchema).toHaveBeenCalledWith(connectorId, tableName)
      expect(result).toBeDefined()
      expect(result.tables).toHaveLength(1)
    })

    it('should handle schema detection errors', async () => {
      const mockError = new Error('Schema detection failed')
      mockError.userMessage = 'Schema detection failed'

      connectorService.detectSchema.mockRejectedValue(mockError)

      const connectorId = 'non-existent'

      const result = await composable.detectSchema(connectorId)

      expect(connectorService.detectSchema).toHaveBeenCalledWith(connectorId, null)
      expect(composable.loading.value).toBe(false)
      expect(composable.error.value).toBe('Schema detection failed')
      expect(result).toBe(null)
    })

    it('should set loading state during schema detection', async () => {
      connectorService.detectSchema.mockResolvedValue({
        success: true,
        schema: { tables: [] }
      })

      const connectorId = 'connector-1'

      const promise = composable.detectSchema(connectorId)

      expect(composable.loading.value).toBe(true)

      await promise

      expect(composable.loading.value).toBe(false)
    })
  })

  describe('detectSchemaPreview', () => {
    it('should successfully detect schema preview for new connector', async () => {
      const mockResponse = {
        success: true,
        message: 'Schema preview detected successfully',
        schema: {
          tables: [
            { name: 'users', columns: [{ name: 'id', type: 'int' }, { name: 'name', type: 'varchar' }] },
            { name: 'orders', columns: [{ name: 'id', type: 'int' }, { name: 'user_id', type: 'int' }] }
          ]
        }
      }

      connectorService.detectSchemaPreview.mockResolvedValue(mockResponse)

      const type = 'Database'
      const provider = 'SqlServer'
      const config = { server: 'localhost', database: 'test' }

      const result = await composable.detectSchemaPreview(type, provider, config)

      expect(connectorService.detectSchemaPreview).toHaveBeenCalledWith(type, provider, config, null)
      expect(composable.loading.value).toBe(false)
      expect(composable.error.value).toBe(null)
      expect(result).toBeDefined()
      expect(result.tables).toHaveLength(2)
      expect(composable.schema.value).toEqual(result)
    })

    it('should detect schema preview with specific table name', async () => {
      const mockResponse = {
        success: true,
        message: 'Schema preview detected successfully',
        schema: {
          tables: [
            { name: 'users', columns: [{ name: 'id', type: 'int' }, { name: 'name', type: 'varchar' }] }
          ]
        }
      }

      connectorService.detectSchemaPreview.mockResolvedValue(mockResponse)

      const type = 'Database'
      const provider = 'SqlServer'
      const config = { server: 'localhost', database: 'test' }
      const tableName = 'users'

      const result = await composable.detectSchemaPreview(type, provider, config, tableName)

      expect(connectorService.detectSchemaPreview).toHaveBeenCalledWith(type, provider, config, tableName)
      expect(result).toBeDefined()
      expect(result.tables).toHaveLength(1)
    })

    it('should handle schema preview detection errors', async () => {
      const mockError = new Error('Schema preview detection failed')
      mockError.userMessage = 'Schema preview detection failed'

      connectorService.detectSchemaPreview.mockRejectedValue(mockError)

      const type = 'Database'
      const provider = 'SqlServer'
      const config = {}

      const result = await composable.detectSchemaPreview(type, provider, config)

      expect(connectorService.detectSchemaPreview).toHaveBeenCalledWith(type, provider, config, null)
      expect(composable.loading.value).toBe(false)
      expect(composable.error.value).toBe('Schema preview detection failed')
      expect(result).toBe(null)
    })

    it('should set loading state during schema preview detection', async () => {
      connectorService.detectSchemaPreview.mockResolvedValue({
        success: true,
        schema: { tables: [] }
      })

      const type = 'Database'
      const provider = 'SqlServer'
      const config = {}

      const promise = composable.detectSchemaPreview(type, provider, config)

      expect(composable.loading.value).toBe(true)

      await promise

      expect(composable.loading.value).toBe(false)
    })
  })

  describe('Error Handling', () => {
    it('should clear previous errors on new operations', async () => {
      // First set an error
      composable.error.value = 'Previous error'

      const mockError = new Error('New error')
      mockError.userMessage = 'New error'

      connectorService.testConnection.mockRejectedValue(mockError)

      const connector = { type: 'Database', provider: 'SqlServer', config: {} }
      await composable.validateConnection(connector)

      expect(composable.error.value).toBe('New error')
      expect(composable.error.value).not.toBe('Previous error')
    })

    it('should handle network errors gracefully', async () => {
      const mockError = new Error('Network error')
      mockError.userMessage = 'Network error'

      connectorService.testConnection.mockRejectedValue(mockError)

      const connector = { type: 'Database', provider: 'SqlServer', config: {} }
      const result = await composable.validateConnection(connector)

      expect(result.success).toBe(false)
      expect(result.message).toBe('Network error')
      expect(composable.error.value).toBe('Network error')
    })
  })
})
