import { http, HttpResponse } from 'msw'

// Mock data generators
import { createMockUser, createMockTenant, createMockConnector, createMockPipeline, createMockSchedule, createMockExecution, createMockAuditLog } from '../utils'

// Base URL for API
const API_BASE = 'http://localhost:5000'

// Helper to get auth token from request headers
const getAuthToken = (request) => {
  const authHeader = request.headers.get('Authorization')
  return authHeader?.replace('Bearer ', '')
}

// Helper to get tenant ID from request headers
const getTenantId = (request) => {
  return request.headers.get('X-Tenant-Id')
}

// Mock storage for stateful operations
let mockUsers = [createMockUser()]
let mockTenants = [createMockTenant()]
let mockConnectors = [createMockConnector()]
let mockPipelines = [createMockPipeline()]
let mockSchedules = [createMockSchedule()]
let mockExecutions = [createMockExecution()]
let mockAuditLogs = [createMockAuditLog()]

// Authentication handlers
export const authHandlers = [
  // POST /connect/token - Token endpoint
  http.post(`${API_BASE}/connect/token`, async ({ request }) => {
    const body = await request.text()
    const params = new URLSearchParams(body)

    const grantType = params.get('grant_type')
    const clientId = params.get('client_id')

    if (grantType === 'authorization_code') {
      // Mock successful token exchange
      return HttpResponse.json({
        access_token: 'mock_access_token_' + Date.now(),
        refresh_token: 'mock_refresh_token_' + Date.now(),
        id_token: 'mock_id_token_' + Date.now(),
        expires_in: 900,
        token_type: 'Bearer'
      })
    }

    if (grantType === 'refresh_token') {
      // Mock successful token refresh
      return HttpResponse.json({
        access_token: 'mock_access_token_refreshed_' + Date.now(),
        refresh_token: 'mock_refresh_token_refreshed_' + Date.now(),
        id_token: 'mock_id_token_refreshed_' + Date.now(),
        expires_in: 900,
        token_type: 'Bearer'
      })
    }

    return HttpResponse.json({ error: 'invalid_grant' }, { status: 400 })
  }),

  // POST /connect/revoke - Revoke token
  http.post(`${API_BASE}/connect/revoke`, () => {
    return HttpResponse.json({ success: true })
  }),

  // POST /api/Account/register
  http.post(`${API_BASE}/api/Account/register`, async ({ request }) => {
    const body = await request.json()
    const newUser = createMockUser({
      email: body.email,
      firstName: body.firstName,
      lastName: body.lastName
    })
    mockUsers.push(newUser)
    return HttpResponse.json(newUser)
  }),

  // POST /api/Account/confirm-email
  http.post(`${API_BASE}/api/Account/confirm-email`, async ({ request }) => {
    const body = await request.json()
    return HttpResponse.json({ message: 'Email confirmed successfully' })
  }),

  // POST /api/Account/forgot-password
  http.post(`${API_BASE}/api/Account/forgot-password`, async ({ request }) => {
    const body = await request.json()
    return HttpResponse.json({ message: 'Password reset email sent' })
  }),

  // POST /api/Account/reset-password
  http.post(`${API_BASE}/api/Account/reset-password`, async ({ request }) => {
    const body = await request.json()
    return HttpResponse.json({ message: 'Password reset successfully' })
  }),

  // POST /api/Account/change-password
  http.post(`${API_BASE}/api/Account/change-password`, async ({ request }) => {
    const token = getAuthToken(request)
    if (!token) {
      return HttpResponse.json({ error: 'Unauthorized' }, { status: 401 })
    }

    const body = await request.json()
    return HttpResponse.json({ message: 'Password changed successfully' })
  }),

  // POST /api/Account/switch-tenant
  http.post(`${API_BASE}/api/Account/switch-tenant`, async ({ request }) => {
    const token = getAuthToken(request)
    if (!token) {
      return HttpResponse.json({ error: 'Unauthorized' }, { status: 401 })
    }

    const body = await request.json()
    const tenant = mockTenants.find(t => t.id === body.tenantId)
    if (!tenant) {
      return HttpResponse.json({ error: 'Tenant not found' }, { status: 404 })
    }

    return HttpResponse.json({
      requiresTokenRefresh: true,
      tenantName: tenant.name
    })
  }),

  // POST /api/Account/logout
  http.post(`${API_BASE}/api/Account/logout`, ({ request }) => {
    const token = getAuthToken(request)
    if (!token) {
      return HttpResponse.json({ error: 'Unauthorized' }, { status: 401 })
    }

    return HttpResponse.json({ message: 'Logged out successfully' })
  })
]

// Tenant handlers
export const tenantHandlers = [
  // GET /api/tenants
  http.get(`${API_BASE}/api/tenants`, ({ request }) => {
    const token = getAuthToken(request)
    if (!token) {
      return HttpResponse.json({ error: 'Unauthorized' }, { status: 401 })
    }

    return HttpResponse.json(mockTenants)
  }),

  // POST /api/tenants
  http.post(`${API_BASE}/api/tenants`, async ({ request }) => {
    const token = getAuthToken(request)
    if (!token) {
      return HttpResponse.json({ error: 'Unauthorized' }, { status: 401 })
    }

    const body = await request.json()
    const newTenant = createMockTenant({
      name: body.name,
      description: body.description
    })
    mockTenants.push(newTenant)
    return HttpResponse.json(newTenant, { status: 201 })
  }),

  // GET /api/tenants/:id
  http.get(`${API_BASE}/api/tenants/:id`, ({ request, params }) => {
    const token = getAuthToken(request)
    if (!token) {
      return HttpResponse.json({ error: 'Unauthorized' }, { status: 401 })
    }

    const tenant = mockTenants.find(t => t.id === params.id)
    if (!tenant) {
      return HttpResponse.json({ error: 'Tenant not found' }, { status: 404 })
    }

    return HttpResponse.json(tenant)
  }),

  // PUT /api/tenants/:id
  http.put(`${API_BASE}/api/tenants/:id`, async ({ request, params }) => {
    const token = getAuthToken(request)
    if (!token) {
      return HttpResponse.json({ error: 'Unauthorized' }, { status: 401 })
    }

    const tenantIndex = mockTenants.findIndex(t => t.id === params.id)
    if (tenantIndex === -1) {
      return HttpResponse.json({ error: 'Tenant not found' }, { status: 404 })
    }

    const body = await request.json()
    mockTenants[tenantIndex] = { ...mockTenants[tenantIndex], ...body }
    return HttpResponse.json(mockTenants[tenantIndex])
  }),

  // DELETE /api/tenants/:id
  http.delete(`${API_BASE}/api/tenants/:id`, ({ request, params }) => {
    const token = getAuthToken(request)
    if (!token) {
      return HttpResponse.json({ error: 'Unauthorized' }, { status: 401 })
    }

    const tenantIndex = mockTenants.findIndex(t => t.id === params.id)
    if (tenantIndex === -1) {
      return HttpResponse.json({ error: 'Tenant not found' }, { status: 404 })
    }

    mockTenants.splice(tenantIndex, 1)
    return HttpResponse.json({ message: 'Tenant deleted successfully' })
  })
]

// Connector handlers
export const connectorHandlers = [
  // GET /api/connectors
  http.get(`${API_BASE}/api/connectors`, ({ request }) => {
    const token = getAuthToken(request)
    const tenantId = getTenantId(request)

    if (!token) {
      return HttpResponse.json({ error: 'Unauthorized' }, { status: 401 })
    }

    if (!tenantId) {
      return HttpResponse.json({ error: 'Tenant ID required' }, { status: 400 })
    }

    return HttpResponse.json(mockConnectors)
  }),

  // POST /api/connectors
  http.post(`${API_BASE}/api/connectors`, async ({ request }) => {
    const token = getAuthToken(request)
    const tenantId = getTenantId(request)

    if (!token) {
      return HttpResponse.json({ error: 'Unauthorized' }, { status: 401 })
    }

    if (!tenantId) {
      return HttpResponse.json({ error: 'Tenant ID required' }, { status: 400 })
    }

    const body = await request.json()
    const newConnector = createMockConnector({
      name: body.name,
      type: body.type,
      provider: body.provider,
      configuration: body.configuration
    })
    mockConnectors.push(newConnector)
    return HttpResponse.json(newConnector, { status: 201 })
  }),

  // GET /api/connectors/:id
  http.get(`${API_BASE}/api/connectors/:id`, ({ request, params }) => {
    const token = getAuthToken(request)
    const tenantId = getTenantId(request)

    if (!token) {
      return HttpResponse.json({ error: 'Unauthorized' }, { status: 401 })
    }

    if (!tenantId) {
      return HttpResponse.json({ error: 'Tenant ID required' }, { status: 400 })
    }

    const connector = mockConnectors.find(c => c.id === params.id)
    if (!connector) {
      return HttpResponse.json({ error: 'Connector not found' }, { status: 404 })
    }

    return HttpResponse.json(connector)
  }),

  // PUT /api/connectors/:id
  http.put(`${API_BASE}/api/connectors/:id`, async ({ request, params }) => {
    const token = getAuthToken(request)
    const tenantId = getTenantId(request)

    if (!token) {
      return HttpResponse.json({ error: 'Unauthorized' }, { status: 401 })
    }

    if (!tenantId) {
      return HttpResponse.json({ error: 'Tenant ID required' }, { status: 400 })
    }

    const connectorIndex = mockConnectors.findIndex(c => c.id === params.id)
    if (connectorIndex === -1) {
      return HttpResponse.json({ error: 'Connector not found' }, { status: 404 })
    }

    const body = await request.json()
    mockConnectors[connectorIndex] = { ...mockConnectors[connectorIndex], ...body }
    return HttpResponse.json(mockConnectors[connectorIndex])
  }),

  // DELETE /api/connectors/:id
  http.delete(`${API_BASE}/api/connectors/:id`, ({ request, params }) => {
    const token = getAuthToken(request)
    const tenantId = getTenantId(request)

    if (!token) {
      return HttpResponse.json({ error: 'Unauthorized' }, { status: 401 })
    }

    if (!tenantId) {
      return HttpResponse.json({ error: 'Tenant ID required' }, { status: 400 })
    }

    const connectorIndex = mockConnectors.findIndex(c => c.id === params.id)
    if (connectorIndex === -1) {
      return HttpResponse.json({ error: 'Connector not found' }, { status: 404 })
    }

    mockConnectors.splice(connectorIndex, 1)
    return HttpResponse.json({ message: 'Connector deleted successfully' })
  }),

  // POST /api/connectors/:id/test
  http.post(`${API_BASE}/api/connectors/:id/test`, ({ request, params }) => {
    const token = getAuthToken(request)
    const tenantId = getTenantId(request)

    if (!token) {
      return HttpResponse.json({ error: 'Unauthorized' }, { status: 401 })
    }

    if (!tenantId) {
      return HttpResponse.json({ error: 'Tenant ID required' }, { status: 400 })
    }

    const connector = mockConnectors.find(c => c.id === params.id)
    if (!connector) {
      return HttpResponse.json({ error: 'Connector not found' }, { status: 404 })
    }

    return HttpResponse.json({
      success: true,
      message: 'Connection test successful'
    })
  }),

  // POST /api/connectors/test-connection
  http.post(`${API_BASE}/api/connectors/test-connection`, async ({ request }) => {
    const token = getAuthToken(request)
    const tenantId = getTenantId(request)

    if (!token) {
      return HttpResponse.json({ error: 'Unauthorized' }, { status: 401 })
    }

    if (!tenantId) {
      return HttpResponse.json({ error: 'Tenant ID required' }, { status: 400 })
    }

    const body = await request.json()

    // Mock successful connection test with schema
    return HttpResponse.json({
      success: true,
      message: 'Connection test successful',
      schema: {
        tables: [
          { name: 'users', columns: [{ name: 'id', type: 'int' }, { name: 'name', type: 'varchar' }] },
          { name: 'orders', columns: [{ name: 'id', type: 'int' }, { name: 'user_id', type: 'int' }] }
        ]
      },
      details: 'Connected successfully to database'
    })
  }),

  // POST /api/connectors/detect-schema
  http.post(`${API_BASE}/api/connectors/detect-schema`, async ({ request }) => {
    const token = getAuthToken(request)
    const tenantId = getTenantId(request)

    if (!token) {
      return HttpResponse.json({ error: 'Unauthorized' }, { status: 401 })
    }

    if (!tenantId) {
      return HttpResponse.json({ error: 'Tenant ID required' }, { status: 400 })
    }

    const body = await request.json()
    const connector = mockConnectors.find(c => c.id === body.connectorId)

    if (!connector) {
      return HttpResponse.json({ error: 'Connector not found' }, { status: 404 })
    }

    return HttpResponse.json({
      success: true,
      message: 'Schema detected successfully',
      schema: {
        tables: [
          { name: 'users', columns: [{ name: 'id', type: 'int' }, { name: 'name', type: 'varchar' }] },
          { name: 'orders', columns: [{ name: 'id', type: 'int' }, { name: 'user_id', type: 'int' }] }
        ]
      }
    })
  }),

  // POST /api/connectors/detect-schema-preview
  http.post(`${API_BASE}/api/connectors/detect-schema-preview`, async ({ request }) => {
    const token = getAuthToken(request)
    const tenantId = getTenantId(request)

    if (!token) {
      return HttpResponse.json({ error: 'Unauthorized' }, { status: 401 })
    }

    if (!tenantId) {
      return HttpResponse.json({ error: 'Tenant ID required' }, { status: 400 })
    }

    const body = await request.json()

    return HttpResponse.json({
      success: true,
      message: 'Schema preview detected successfully',
      schema: {
        tables: [
          { name: 'users', columns: [{ name: 'id', type: 'int' }, { name: 'name', type: 'varchar' }] },
          { name: 'orders', columns: [{ name: 'id', type: 'int' }, { name: 'user_id', type: 'int' }] }
        ]
      }
    })
  })
]

// Pipeline handlers
export const pipelineHandlers = [
  // GET /api/pipelines
  http.get(`${API_BASE}/api/pipelines`, ({ request }) => {
    const token = getAuthToken(request)
    const tenantId = getTenantId(request)

    if (!token) {
      return HttpResponse.json({ error: 'Unauthorized' }, { status: 401 })
    }

    if (!tenantId) {
      return HttpResponse.json({ error: 'Tenant ID required' }, { status: 400 })
    }

    return HttpResponse.json(mockPipelines)
  }),

  // POST /api/pipelines
  http.post(`${API_BASE}/api/pipelines`, async ({ request }) => {
    const token = getAuthToken(request)
    const tenantId = getTenantId(request)

    if (!token) {
      return HttpResponse.json({ error: 'Unauthorized' }, { status: 401 })
    }

    if (!tenantId) {
      return HttpResponse.json({ error: 'Tenant ID required' }, { status: 400 })
    }

    const body = await request.json()
    const newPipeline = createMockPipeline({
      name: body.name,
      description: body.description,
      sourceConnectorId: body.sourceConnectorId,
      destinationConnectorId: body.destinationConnectorId,
      transformations: body.transformations
    })
    mockPipelines.push(newPipeline)
    return HttpResponse.json(newPipeline, { status: 201 })
  }),

  // GET /api/pipelines/:id
  http.get(`${API_BASE}/api/pipelines/:id`, ({ request, params }) => {
    const token = getAuthToken(request)
    const tenantId = getTenantId(request)

    if (!token) {
      return HttpResponse.json({ error: 'Unauthorized' }, { status: 401 })
    }

    if (!tenantId) {
      return HttpResponse.json({ error: 'Tenant ID required' }, { status: 400 })
    }

    const pipeline = mockPipelines.find(p => p.id === params.id)
    if (!pipeline) {
      return HttpResponse.json({ error: 'Pipeline not found' }, { status: 404 })
    }

    return HttpResponse.json(pipeline)
  }),

  // PUT /api/pipelines/:id
  http.put(`${API_BASE}/api/pipelines/:id`, async ({ request, params }) => {
    const token = getAuthToken(request)
    const tenantId = getTenantId(request)

    if (!token) {
      return HttpResponse.json({ error: 'Unauthorized' }, { status: 401 })
    }

    if (!tenantId) {
      return HttpResponse.json({ error: 'Tenant ID required' }, { status: 400 })
    }

    const pipelineIndex = mockPipelines.findIndex(p => p.id === params.id)
    if (pipelineIndex === -1) {
      return HttpResponse.json({ error: 'Pipeline not found' }, { status: 404 })
    }

    const body = await request.json()
    mockPipelines[pipelineIndex] = { ...mockPipelines[pipelineIndex], ...body }
    return HttpResponse.json(mockPipelines[pipelineIndex])
  }),

  // DELETE /api/pipelines/:id
  http.delete(`${API_BASE}/api/pipelines/:id`, ({ request, params }) => {
    const token = getAuthToken(request)
    const tenantId = getTenantId(request)

    if (!token) {
      return HttpResponse.json({ error: 'Unauthorized' }, { status: 401 })
    }

    if (!tenantId) {
      return HttpResponse.json({ error: 'Tenant ID required' }, { status: 400 })
    }

    const pipelineIndex = mockPipelines.findIndex(p => p.id === params.id)
    if (pipelineIndex === -1) {
      return HttpResponse.json({ error: 'Pipeline not found' }, { status: 404 })
    }

    mockPipelines.splice(pipelineIndex, 1)
    return HttpResponse.json({ message: 'Pipeline deleted successfully' })
  }),

  // POST /api/pipelines/:id/execute
  http.post(`${API_BASE}/api/pipelines/:id/execute`, ({ request, params }) => {
    const token = getAuthToken(request)
    const tenantId = getTenantId(request)

    if (!token) {
      return HttpResponse.json({ error: 'Unauthorized' }, { status: 401 })
    }

    if (!tenantId) {
      return HttpResponse.json({ error: 'Tenant ID required' }, { status: 400 })
    }

    const pipeline = mockPipelines.find(p => p.id === params.id)
    if (!pipeline) {
      return HttpResponse.json({ error: 'Pipeline not found' }, { status: 404 })
    }

    const newExecution = createMockExecution({
      pipelineId: params.id,
      status: 'running'
    })
    mockExecutions.push(newExecution)

    return HttpResponse.json(newExecution, { status: 201 })
  })
]

// Schedule handlers
export const scheduleHandlers = [
  // GET /api/schedules
  http.get(`${API_BASE}/api/schedules`, ({ request }) => {
    const token = getAuthToken(request)
    const tenantId = getTenantId(request)

    if (!token) {
      return HttpResponse.json({ error: 'Unauthorized' }, { status: 401 })
    }

    if (!tenantId) {
      return HttpResponse.json({ error: 'Tenant ID required' }, { status: 400 })
    }

    return HttpResponse.json(mockSchedules)
  }),

  // POST /api/schedules
  http.post(`${API_BASE}/api/schedules`, async ({ request }) => {
    const token = getAuthToken(request)
    const tenantId = getTenantId(request)

    if (!token) {
      return HttpResponse.json({ error: 'Unauthorized' }, { status: 401 })
    }

    if (!tenantId) {
      return HttpResponse.json({ error: 'Tenant ID required' }, { status: 400 })
    }

    const body = await request.json()
    const newSchedule = createMockSchedule({
      pipelineId: body.pipelineId,
      name: body.name,
      cronExpression: body.cronExpression,
      isActive: body.isActive
    })
    mockSchedules.push(newSchedule)
    return HttpResponse.json(newSchedule, { status: 201 })
  }),

  // GET /api/schedules/:id
  http.get(`${API_BASE}/api/schedules/:id`, ({ request, params }) => {
    const token = getAuthToken(request)
    const tenantId = getTenantId(request)

    if (!token) {
      return HttpResponse.json({ error: 'Unauthorized' }, { status: 401 })
    }

    if (!tenantId) {
      return HttpResponse.json({ error: 'Tenant ID required' }, { status: 400 })
    }

    const schedule = mockSchedules.find(s => s.id === params.id)
    if (!schedule) {
      return HttpResponse.json({ error: 'Schedule not found' }, { status: 404 })
    }

    return HttpResponse.json(schedule)
  }),

  // PUT /api/schedules/:id
  http.put(`${API_BASE}/api/schedules/:id`, async ({ request, params }) => {
    const token = getAuthToken(request)
    const tenantId = getTenantId(request)

    if (!token) {
      return HttpResponse.json({ error: 'Unauthorized' }, { status: 401 })
    }

    if (!tenantId) {
      return HttpResponse.json({ error: 'Tenant ID required' }, { status: 400 })
    }

    const scheduleIndex = mockSchedules.findIndex(s => s.id === params.id)
    if (scheduleIndex === -1) {
      return HttpResponse.json({ error: 'Schedule not found' }, { status: 404 })
    }

    const body = await request.json()
    mockSchedules[scheduleIndex] = { ...mockSchedules[scheduleIndex], ...body }
    return HttpResponse.json(mockSchedules[scheduleIndex])
  }),

  // DELETE /api/schedules/:id
  http.delete(`${API_BASE}/api/schedules/:id`, ({ request, params }) => {
    const token = getAuthToken(request)
    const tenantId = getTenantId(request)

    if (!token) {
      return HttpResponse.json({ error: 'Unauthorized' }, { status: 401 })
    }

    if (!tenantId) {
      return HttpResponse.json({ error: 'Tenant ID required' }, { status: 400 })
    }

    const scheduleIndex = mockSchedules.findIndex(s => s.id === params.id)
    if (scheduleIndex === -1) {
      return HttpResponse.json({ error: 'Schedule not found' }, { status: 404 })
    }

    mockSchedules.splice(scheduleIndex, 1)
    return HttpResponse.json({ message: 'Schedule deleted successfully' })
  })
]

// Execution handlers
export const executionHandlers = [
  // GET /api/executions
  http.get(`${API_BASE}/api/executions`, ({ request }) => {
    const token = getAuthToken(request)
    const tenantId = getTenantId(request)

    if (!token) {
      return HttpResponse.json({ error: 'Unauthorized' }, { status: 401 })
    }

    if (!tenantId) {
      return HttpResponse.json({ error: 'Tenant ID required' }, { status: 400 })
    }

    return HttpResponse.json(mockExecutions)
  }),

  // GET /api/executions/:id
  http.get(`${API_BASE}/api/executions/:id`, ({ request, params }) => {
    const token = getAuthToken(request)
    const tenantId = getTenantId(request)

    if (!token) {
      return HttpResponse.json({ error: 'Unauthorized' }, { status: 401 })
    }

    if (!tenantId) {
      return HttpResponse.json({ error: 'Tenant ID required' }, { status: 400 })
    }

    const execution = mockExecutions.find(e => e.id === params.id)
    if (!execution) {
      return HttpResponse.json({ error: 'Execution not found' }, { status: 404 })
    }

    return HttpResponse.json(execution)
  }),

  // PUT /api/executions/:id/cancel
  http.put(`${API_BASE}/api/executions/:id/cancel`, ({ request, params }) => {
    const token = getAuthToken(request)
    const tenantId = getTenantId(request)

    if (!token) {
      return HttpResponse.json({ error: 'Unauthorized' }, { status: 401 })
    }

    if (!tenantId) {
      return HttpResponse.json({ error: 'Tenant ID required' }, { status: 400 })
    }

    const executionIndex = mockExecutions.findIndex(e => e.id === params.id)
    if (executionIndex === -1) {
      return HttpResponse.json({ error: 'Execution not found' }, { status: 404 })
    }

    mockExecutions[executionIndex].status = 'cancelled'
    mockExecutions[executionIndex].endedAt = new Date().toISOString()

    return HttpResponse.json(mockExecutions[executionIndex])
  })
]

// Dashboard handlers
export const dashboardHandlers = [
  // GET /api/executions/stats
  http.get(`${API_BASE}/api/executions/stats`, ({ request }) => {
    const token = getAuthToken(request)
    const tenantId = getTenantId(request)

    if (!token) {
      return HttpResponse.json({ error: 'Unauthorized' }, { status: 401 })
    }

    if (!tenantId) {
      return HttpResponse.json({ error: 'Tenant ID required' }, { status: 400 })
    }

    // Mock execution stats
    const stats = {
      totalExecutions: 150,
      runningExecutions: 3,
      completedExecutions: 120,
      failedExecutions: 15,
      cancelledExecutions: 12,
      successRate: 80.0,
      averageDurationMs: 45000,
      totalRecordsProcessed: 125000,
      lastExecutionTime: new Date().toISOString()
    }

    return HttpResponse.json(stats)
  })
]

// Audit log handlers
export const auditLogHandlers = [
  // GET /api/audit-logs
  http.get(`${API_BASE}/api/audit-logs`, ({ request }) => {
    const token = getAuthToken(request)
    const tenantId = getTenantId(request)

    if (!token) {
      return HttpResponse.json({ error: 'Unauthorized' }, { status: 401 })
    }

    if (!tenantId) {
      return HttpResponse.json({ error: 'Tenant ID required' }, { status: 400 })
    }

    return HttpResponse.json(mockAuditLogs)
  })
]

// User handlers
export const userHandlers = [
  // GET /api/users
  http.get(`${API_BASE}/api/users`, ({ request }) => {
    const token = getAuthToken(request)
    const tenantId = getTenantId(request)

    if (!token) {
      return HttpResponse.json({ error: 'Unauthorized' }, { status: 401 })
    }

    if (!tenantId) {
      return HttpResponse.json({ error: 'Tenant ID required' }, { status: 400 })
    }

    return HttpResponse.json(mockUsers)
  }),

  // POST /api/users
  http.post(`${API_BASE}/api/users`, async ({ request }) => {
    const token = getAuthToken(request)
    const tenantId = getTenantId(request)

    if (!token) {
      return HttpResponse.json({ error: 'Unauthorized' }, { status: 401 })
    }

    if (!tenantId) {
      return HttpResponse.json({ error: 'Tenant ID required' }, { status: 400 })
    }

    const body = await request.json()
    const newUser = createMockUser({
      email: body.email,
      firstName: body.firstName,
      lastName: body.lastName,
      role: body.role
    })
    mockUsers.push(newUser)
    return HttpResponse.json(newUser, { status: 201 })
  }),

  // GET /api/users/:id
  http.get(`${API_BASE}/api/users/:id`, ({ request, params }) => {
    const token = getAuthToken(request)
    const tenantId = getTenantId(request)

    if (!token) {
      return HttpResponse.json({ error: 'Unauthorized' }, { status: 401 })
    }

    if (!tenantId) {
      return HttpResponse.json({ error: 'Tenant ID required' }, { status: 400 })
    }

    const user = mockUsers.find(u => u.id === params.id)
    if (!user) {
      return HttpResponse.json({ error: 'User not found' }, { status: 404 })
    }

    return HttpResponse.json(user)
  }),

  // PUT /api/users/:id
  http.put(`${API_BASE}/api/users/:id`, async ({ request, params }) => {
    const token = getAuthToken(request)
    const tenantId = getTenantId(request)

    if (!token) {
      return HttpResponse.json({ error: 'Unauthorized' }, { status: 401 })
    }

    if (!tenantId) {
      return HttpResponse.json({ error: 'Tenant ID required' }, { status: 400 })
    }

    const userIndex = mockUsers.findIndex(u => u.id === params.id)
    if (userIndex === -1) {
      return HttpResponse.json({ error: 'User not found' }, { status: 404 })
    }

    const body = await request.json()
    mockUsers[userIndex] = { ...mockUsers[userIndex], ...body }
    return HttpResponse.json(mockUsers[userIndex])
  }),

  // DELETE /api/users/:id
  http.delete(`${API_BASE}/api/users/:id`, ({ request, params }) => {
    const token = getAuthToken(request)
    const tenantId = getTenantId(request)

    if (!token) {
      return HttpResponse.json({ error: 'Unauthorized' }, { status: 401 })
    }

    if (!tenantId) {
      return HttpResponse.json({ error: 'Tenant ID required' }, { status: 400 })
    }

    const userIndex = mockUsers.findIndex(u => u.id === params.id)
    if (userIndex === -1) {
      return HttpResponse.json({ error: 'User not found' }, { status: 404 })
    }

    mockUsers.splice(userIndex, 1)
    return HttpResponse.json({ message: 'User deleted successfully' })
  })
]

// Export all handlers
export const handlers = [
  ...authHandlers,
  ...tenantHandlers,
  ...connectorHandlers,
  ...pipelineHandlers,
  ...scheduleHandlers,
  ...executionHandlers,
  ...dashboardHandlers,
  ...auditLogHandlers,
  ...userHandlers
]