export const API_CONFIG = {
  baseURL: import.meta.env.VITE_API_BASE_URL || 'http://localhost:5244',
  timeout: 30000,
  headers: {
    'Content-Type': 'application/json'
  }
}

export const API_ENDPOINTS = {
  auth: {
    // OAuth 2.0 / OpenIddict endpoints (at root level, no /api prefix)
    token: '/connect/token',
    revoke: '/connect/revoke',
    authorize: '/connect/authorize',
    // Account management endpoints (under /api prefix)
    register: '/api/Account/register',
    confirmEmail: '/api/Account/confirm-email',
    forgotPassword: '/api/Account/forgot-password',
    resetPassword: '/api/Account/reset-password',
    changePassword: '/api/Account/change-password',
    logout: '/api/Account/logout',
    switchTenant: '/api/Account/switch-tenant'
  },
  tenants: {
    base: '/api/tenants',
    byId: (id) => `/api/tenants/${id}`
  },
  users: {
    base: '/api/users',
    byId: (id) => `/api/users/${id}`,
    toggleStatus: (id) => `/api/users/${id}/toggle-status`
  },
  transformations: {
    base: '/api/transformations',
    byId: (id) => `/api/transformations/${id}`
  },
  pipelines: {
    base: '/api/pipelines',
    byId: (id) => `/api/pipelines/${id}`,
    executions: '/api/executions',
    executionById: (id) => `/api/executions/${id}`
  },
  connectors: {
    base: '/api/connectors',
    byId: (id) => `/api/connectors/${id}`
  },
  schedules: {
    base: '/api/schedules',
    byId: (id) => `/api/schedules/${id}`,
    byPipelineId: (pipelineId) => `/api/schedules/pipeline/${pipelineId}`,
    enable: (id) => `/api/schedules/${id}/enable`,
    disable: (id) => `/api/schedules/${id}/disable`,
    trigger: (id) => `/api/schedules/${id}/trigger`,
    validateCron: '/api/schedules/validate-cron'
  }
}