import api from './api'

/**
 * Fetches the list of pipelines with optional filtering
 * @param {Object} filters - Filter parameters
 * @param {string} filters.search - Search term to filter pipelines
 * @param {string} filters.status - Status to filter by
 * @param {string} filters.sortBy - Sort field and direction (e.g., 'name_asc')
 * @returns {Promise<Array>} List of pipeline objects
 */
export async function fetchPipelines(filters = {}) {
const params = {
      page: filters.page || 1,
      pageSize: filters.pageSize || 20
    }

    if (filters.search) {
      params.search = filters.search
    }

    if (filters.name) {
      params.name = filters.name
    }

    if (filters.status && filters.status !== 'All') {
      params.status = filters.status
    }

    if (filters.isScheduled !== undefined) {
      params.isScheduled = filters.isScheduled
    }

    if (filters.isActive !== undefined) {
      params.isActive = filters.isActive
    }

    if (filters.sortBy) {
      params.sortBy = filters.sortBy
    }

    const response = await api.get('/api/pipelines', { params })
    // Backend returns PagedPipelineResponse with pipelines property (camelCase due to JSON config)
    return response.data.pipelines || []
}

/**
 * Fetches a single pipeline by ID
 * @param {string} id - Pipeline ID
 * @returns {Promise<Object>} Pipeline object
 */
export async function fetchPipelineById(id) {
const response = await api.get(`/api/pipelines/${id}`)
    return response.data
}

/**
 * Creates or updates a pipeline
 * @param {Object} pipeline - Pipeline data
 * @returns {Promise<Object>} Created/updated pipeline
 */
export async function savePipeline(pipeline) {
if (pipeline.id) {
      // Update existing pipeline
      const payload = {
        name: pipeline.name,
        description: pipeline.description || null,
        fieldMappings: pipeline.fieldMappings || [],
        schedule: pipeline.schedule || null,
        isScheduled: pipeline.isScheduled || false,
        isActive: pipeline.isActive
      }
      
      const response = await api.put(`/api/pipelines/${pipeline.id}`, payload)
      return response.data
    } else {
      // Create new pipeline
      const payload = {
        name: pipeline.name,
        description: pipeline.description || null,
        sourceConnectorId: pipeline.sourceId,
        destinationConnectorId: pipeline.destinationId,
        fieldMappings: pipeline.fieldMappings || [],
        schedule: pipeline.schedule || null,
        isScheduled: pipeline.isScheduled || false,
        isActive: pipeline.isActive ?? true  // Default to active when creating
      }
      
      const response = await api.post('/api/pipelines', payload)
      return response.data
    }
}

/**
 * Deletes a pipeline
 * @param {string} id - Pipeline ID to delete
 * @returns {Promise<boolean>} Success indicator
 */
export async function deletePipeline(id) {
await api.delete(`/api/pipelines/${id}`)
    return true
}

/**
 * Toggles the active status of a pipeline
 * @param {string} id - Pipeline ID to toggle
 * @returns {Promise<Object>} Updated pipeline object
 */
export async function togglePipelineStatus(id) {
const response = await api.post(`/api/pipelines/${id}/toggle-status`)
    return response.data
}

/**
 * Executes a pipeline
 * @param {string} id - Pipeline ID to execute
 * @returns {Promise<Object>} Execution result
 */
export async function executePipeline(id) {
const response = await api.post(`/api/pipelines/${id}/execute`)
    return response.data
}

/**
 * Gets a list of pipeline executions with optional filters
 * @param {Object} filters - Filter parameters
 * @param {string} filters.pipelineId - Filter by pipeline ID
 * @param {string} filters.status - Filter by execution status
 * @param {Date} filters.startDate - Filter by execution start date (minimum)
 * @param {Date} filters.endDate - Filter by execution start date (maximum)
 * @param {number} filters.page - Page number (default: 1)
 * @param {number} filters.pageSize - Page size (default: 20)
 * @returns {Promise<Array>} List of execution objects
 */
export async function getExecutions(filters = {}) {
const params = {
      page: filters.page || 1,
      pageSize: filters.pageSize || 20
    }
    
    if (filters.pipelineId) {
      params.pipelineId = filters.pipelineId
    }
    
    if (filters.status && filters.status !== 'All') {
      params.status = filters.status
    }
    
    if (filters.startDate) {
      params.startDate = filters.startDate
    }
    
    if (filters.endDate) {
      params.endDate = filters.endDate
    }
    
    if (filters.search) {
      params.search = filters.search
    }
    
    const response = await api.get('/api/executions', { params })
    return response.data.executions || []
}

/**
 * Gets a specific execution by ID
 * @param {string} id - Execution ID
 * @returns {Promise<Object>} Execution object
 */
export async function getExecutionById(id) {
const response = await api.get(`/api/executions/${id}`)
    return response.data
}

/**
 * Cancels a running execution
 * @param {string} id - Execution ID to cancel
 * @returns {Promise<Object>} Updated execution object
 */
export async function cancelExecution(id) {
const response = await api.post(`/api/executions/${id}/cancel`)
    return response.data
}

/**
 * Gets execution statistics
 * @param {string} pipelineId - Optional pipeline ID to filter stats
 * @returns {Promise<Object>} Execution statistics
 */
export async function getExecutionStats(pipelineId = null) {
const params = {}
    if (pipelineId) {
      params.pipelineId = pipelineId
    }
    const response = await api.get('/api/executions/stats', { params })
    return response.data
}

/**
 * Finds all pipelines that use a specific connector
 * @param {string} connectorId - Connector ID
 * @returns {Promise<Array>} List of pipelines using the connector
 */
export async function findPipelinesUsingConnector(connectorId) {
// This will filter pipelines by connector on the backend
    const response = await api.get('/api/pipelines', {
      params: {
        sourceConnectorId: connectorId,
        destinationConnectorId: connectorId
      }
    })
    return response.data.pipelines || []
}

// For backward compatibility with code that might use the old object-based API
export const pipelineService = {
  getAll: fetchPipelines,
  getById: fetchPipelineById,
  create: pipeline => savePipeline(pipeline),
  update: (id, pipelineData) => {
    const pipeline = { id, ...pipelineData };
    return savePipeline(pipeline);
  },
  delete: deletePipeline,
  execute: executePipeline,
  getExecutions,
  getExecutionById,
  cancelExecution,
  getExecutionStats,
  findPipelinesUsingConnector
};



