import api from './api'

/**
 * Fetches the list of connectors with optional filtering
 * @param {Object} filters - Filter parameters
 * @param {string} filters.search - Search term to filter connectors
 * @param {string} filters.type - Type to filter by (Database, File, API)
 * @param {string} filters.provider - Provider to filter by
 * @param {string} filters.direction - Direction to filter by (source, destination, both)
 * @param {boolean} filters.isActive - Active status filter
 * @param {number} filters.page - Page number (default: 1)
 * @param {number} filters.pageSize - Page size (default: 20)
 * @returns {Promise<Object>} Paginated list of connector objects
 */
export async function fetchConnectors(filters = {}) {
  const response = await api.post('/api/connectors/search', {
    name: filters.search || null,
    type: filters.type && filters.type !== 'All' ? filters.type : null,
    provider: filters.provider || null,
    direction: filters.direction || null,
    isActive: filters.isActive,
    page: filters.page || 1,
    pageSize: filters.pageSize || 20
  })

  return response.data
}

/**
 * Fetches a single connector by ID
 * @param {string} id - Connector ID
 * @returns {Promise<Object>} Connector object
 */
export async function fetchConnectorById(id) {
  const response = await api.get(`/api/connectors/${id}`)
  return response.data
}

/**
 * Creates a new connector
 * @param {Object} connector - Connector data
 * @returns {Promise<Object>} Created connector
 */
export async function createConnector(connector) {
  const response = await api.post('/api/connectors', {
    name: connector.name,
    description: connector.description || null,
    type: connector.type,
    provider: connector.provider,
    direction: connector.direction,
    config: connector.config,
    schema: connector.schema || null
  })
  return response.data
}

/**
 * Updates an existing connector
 * @param {string} id - Connector ID
 * @param {Object} connector - Connector data
 * @returns {Promise<Object>} Updated connector
 */
export async function updateConnector(id, connector) {
const response = await api.put(`/api/connectors/${id}`, {
      name: connector.name,
      description: connector.description || null,
      direction: connector.direction,
      config: connector.config,
      schema: connector.schema || null,
      isActive: connector.isActive
    })
    return response.data
}

/**
 * Deletes a connector
 * @param {string} id - Connector ID to delete
 * @returns {Promise<boolean>} Success indicator
 */
export async function deleteConnector(id) {
await api.delete(`/api/connectors/${id}`)
    return true
}

/**
 * Tests a new connection configuration
 * @param {Object} connector - Connector configuration to test
 * @returns {Promise<Object>} Connection test result
 */
export async function testConnection(connector) {
const payload = {
      type: connector.type,
      provider: connector.provider,
      config: connector.config
    };
    const response = await api.post('/api/connectors/test-connection', payload)
    return response.data
}

/**
 * Tests an existing connector's connection
 * @param {string} id - Connector ID
 * @returns {Promise<Object>} Connection test result
 */
export async function testExistingConnection(id) {
const response = await api.post(`/api/connectors/${id}/test`)
    return response.data
}

/**
 * Detects the schema of a connector
 * @param {string} connectorId - Connector ID
 * @param {string} tableOrResourceName - Table name (for databases) or resource name
 * @returns {Promise<Object>} Schema information
 */
export async function detectSchema(connectorId, tableOrResourceName = null) {
const response = await api.post('/api/connectors/detect-schema', {
      connectorId,
      tableOrResourceName
    })
    return response.data
}

/**
 * Detects the schema from connection configuration (for new connectors before saving)
 * @param {string} type - Connector type (Database, File, API)
 * @param {string} provider - Provider (SqlServer, PostgreSQL, MySQL, etc.)
 * @param {Object} config - Connection configuration
 * @param {string} tableOrResourceName - Table name (for databases) or resource name
 * @returns {Promise<Object>} Schema information
 */
export async function detectSchemaPreview(type, provider, config, tableOrResourceName = null) {
const response = await api.post('/api/connectors/detect-schema-preview', {
      type,
      provider,
      config,
      tableOrResourceName
    })
    return response.data
}

/**
 * Gets all active connectors (simplified list)
 * @returns {Promise<Array>} List of all active connectors
 */
export async function getAllConnectors() {
const response = await api.get('/api/connectors')
    return response.data
}

/**
 * Gets connectors that can be used as sources (for pipeline input)
 * @returns {Promise<Array>} List of source connectors
 */
export async function getSourceConnectors() {
const connectors = await getAllConnectors()
    return connectors.filter(c => c.isSource)
}

/**
 * Gets connectors that can be used as destinations (for pipeline output)
 * @returns {Promise<Array>} List of destination connectors
 */
export async function getDestinationConnectors() {
const connectors = await getAllConnectors()
    return connectors.filter(c => c.isDestination)
}

export const connectorService = {
  search: fetchConnectors,
  getAll: getAllConnectors,
  getById: fetchConnectorById,
  create: createConnector,
  update: updateConnector,
  delete: deleteConnector,
  testConnection,
  testExisting: testExistingConnection,
  detectSchema,
  detectSchemaPreview,
  getSources: getSourceConnectors,
  getDestinations: getDestinationConnectors
}

