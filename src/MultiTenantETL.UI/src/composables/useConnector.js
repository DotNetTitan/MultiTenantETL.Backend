import { ref } from 'vue'
import { connectorService } from '@/services/connectorService'

export function useConnector() {
  const loading = ref(false)
  const error = ref(null)
  const schema = ref(null)
  
  const validateConnection = async (connector) => {
    loading.value = true
    error.value = null
    
    try {
      const response = await connectorService.testConnection(connector)
      if (response.schema) {
        schema.value = response.schema
      }
      return {
        success: response.success,
        message: response.message,
        schema: response.schema,
        details: response.details
      }
    } catch (err) {
      error.value = err.userMessage || err.message
      return {
        success: false,
        message: err.userMessage || err.message
      }
    } finally {
      loading.value = false
    }
  }

  const testExistingConnection = async (connectorId) => {
    loading.value = true
    error.value = null
    
    try {
      const response = await connectorService.testExisting(connectorId)
      return {
        success: response.success,
        message: response.message,
        details: response.details
      }
    } catch (err) {
      error.value = err.userMessage || err.message
      return {
        success: false,
        message: err.userMessage || err.message
      }
    } finally {
      loading.value = false
    }
  }

  const detectSchema = async (connectorId, tableOrResourceName = null) => {
    loading.value = true
    error.value = null
    
    try {
      const response = await connectorService.detectSchema(connectorId, tableOrResourceName)
      if (response.success && response.schema) {
        schema.value = response.schema
        return response.schema
      }
      error.value = response.message
      return null
    } catch (err) {
      error.value = err.userMessage || err.message
      return null
    } finally {
      loading.value = false
    }
  }

  const detectSchemaPreview = async (type, provider, config, tableOrResourceName = null) => {
    loading.value = true
    error.value = null
    
    try {
      const response = await connectorService.detectSchemaPreview(type, provider, config, tableOrResourceName)
      if (response.success && response.schema) {
        schema.value = response.schema
        return response.schema
      }
      error.value = response.message
      return null
    } catch (err) {
      error.value = err.userMessage || err.message
      return null
    } finally {
      loading.value = false
    }
  }

  return {
    loading,
    error,
    schema,
    validateConnection,
    testExistingConnection,
    detectSchema,
    detectSchemaPreview
  }
}