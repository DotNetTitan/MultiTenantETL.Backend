import { createApp } from 'vue'
import { createPinia } from 'pinia'
import { createRouter, createMemoryHistory } from 'vue-router'
import { createI18n } from 'vue-i18n'
import { createVuetify } from 'vuetify'
import * as components from 'vuetify/components'
import * as directives from 'vuetify/directives'

// Test utilities for Vue components
export const createTestApp = (component, options = {}) => {
  const {
    props = {},
    global = {},
    routes = [],
    initialRoute = '/',
    pinia = true,
    i18n = true,
    vuetify = true
  } = options

  const app = createApp(component, props)

  // Setup Pinia store
  if (pinia) {
    const piniaInstance = createPinia()
    app.use(piniaInstance)
  }

  // Setup Vue Router
  if (routes.length > 0) {
    const router = createRouter({
      history: createMemoryHistory(),
      routes
    })
    app.use(router)

    if (initialRoute !== '/') {
      router.push(initialRoute)
    }
  }

  // Setup Vue I18n
  if (i18n) {
    const i18nInstance = createI18n({
      legacy: false,
      locale: 'en',
      messages: {
        en: {},
        es: {},
        fr: {},
        de: {},
        it: {},
        pt: {}
      }
    })
    app.use(i18nInstance)
  }

  // Setup Vuetify
  if (vuetify) {
    const vuetifyInstance = createVuetify({
      components,
      directives
    })
    app.use(vuetifyInstance)
  }

  // Apply global mocks
  if (global.mocks) {
    Object.entries(global.mocks).forEach(([key, value]) => {
      app.config.global.mocks[key] = value
    })
  }

  // Apply global stubs
  if (global.stubs) {
    Object.entries(global.stubs).forEach(([key, value]) => {
      app.config.global.stubs[key] = value
    })
  }

  return app
}

// Helper to wait for next tick
export const nextTick = () => new Promise(resolve => setTimeout(resolve, 0))

// Mock data generators
export const createMockUser = (overrides = {}) => ({
  id: 1,
  email: 'test@example.com',
  name: 'Test User',
  role: 'user',
  isActive: true,
  ...overrides
})

export const createMockTenant = (overrides = {}) => ({
  id: 1,
  name: 'Test Tenant',
  domain: 'test.com',
  isActive: true,
  settings: {},
  ...overrides
})

export const createMockConnector = (overrides = {}) => ({
  id: 1,
  name: 'Test Connector',
  type: 'database',
  config: { host: 'localhost', port: 5432 },
  isActive: true,
  ...overrides
})

export const createMockPipeline = (overrides = {}) => ({
  id: 1,
  name: 'Test Pipeline',
  description: 'A test pipeline',
  status: 'active',
  steps: [],
  createdAt: new Date().toISOString(),
  updatedAt: new Date().toISOString(),
  ...overrides
})

// Common test data
export const mockUsers = [
  createMockUser({ id: 1, email: 'admin@test.com', role: 'admin' }),
  createMockUser({ id: 2, email: 'user@test.com', role: 'user' })
]

export const mockTenants = [
  createMockTenant({ id: 1, name: 'Primary Tenant' }),
  createMockTenant({ id: 2, name: 'Secondary Tenant' })
]

export const mockConnectors = [
  createMockConnector({ id: 1, name: 'PostgreSQL DB', type: 'database' }),
  createMockConnector({ id: 2, name: 'REST API', type: 'api' })
]

export const mockPipelines = [
  createMockPipeline({ id: 1, name: 'Data Import Pipeline', status: 'active' }),
  createMockPipeline({ id: 2, name: 'Data Export Pipeline', status: 'draft' })
]

// Mock data generators for schedules
export const createMockSchedule = (overrides = {}) => ({
  id: 1,
  name: 'Daily Data Sync',
  description: 'Synchronize data daily at 2 AM',
  pipelineId: 1,
  cronExpression: '0 2 * * *',
  isActive: true,
  lastRun: new Date().toISOString(),
  nextRun: new Date(Date.now() + 24 * 60 * 60 * 1000).toISOString(),
  createdAt: new Date().toISOString(),
  updatedAt: new Date().toISOString(),
  ...overrides
})

// Mock data generators for executions
export const createMockExecution = (overrides = {}) => ({
  id: 1,
  pipelineId: 1,
  status: 'completed',
  startedAt: new Date(Date.now() - 30 * 60 * 1000).toISOString(),
  completedAt: new Date().toISOString(),
  duration: 1800000, // 30 minutes in milliseconds
  recordsProcessed: 1000,
  recordsFailed: 5,
  errorMessage: null,
  logs: 'Execution completed successfully',
  createdAt: new Date().toISOString(),
  updatedAt: new Date().toISOString(),
  ...overrides
})

// Mock data generators for audit logs
export const createMockAuditLog = (overrides = {}) => ({
  id: 1,
  userId: 1,
  action: 'pipeline_execution',
  resourceType: 'pipeline',
  resourceId: 1,
  details: 'Executed pipeline successfully',
  ipAddress: '192.168.1.100',
  userAgent: 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36',
  timestamp: new Date().toISOString(),
  ...overrides
})

// Additional mock data arrays
export const mockSchedules = [
  createMockSchedule({ id: 1, name: 'Daily Sync', pipelineId: 1 }),
  createMockSchedule({ id: 2, name: 'Weekly Report', pipelineId: 2, cronExpression: '0 9 * * 1' })
]

export const mockExecutions = [
  createMockExecution({ id: 1, pipelineId: 1, status: 'completed' }),
  createMockExecution({ id: 2, pipelineId: 2, status: 'running', completedAt: null })
]

export const mockAuditLogs = [
  createMockAuditLog({ id: 1, action: 'pipeline_execution', resourceId: 1 }),
  createMockAuditLog({ id: 2, action: 'user_login', resourceType: 'user', resourceId: 2 })
]