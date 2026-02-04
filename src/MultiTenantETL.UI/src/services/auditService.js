import api from './api'

export const auditService = {
  /**
   * Get audit logs with filtering
   */
  async getAuditLogs(filters = {}) {
    const params = {}
    
    if (filters.userId) params.userId = filters.userId
    if (filters.action) params.action = filters.action
    if (filters.resourceType) params.resourceType = filters.resourceType
    if (filters.severity) params.severity = filters.severity
    if (filters.startDate) params.startDate = filters.startDate
    if (filters.endDate) params.endDate = filters.endDate
    
    params.page = filters.page || 1
    params.pageSize = filters.pageSize || 10

    const response = await api.get('/api/AuditLogs', { params })
    return response.data
  },

  /**
   * Get current user's audit logs
   */
  async getMyAuditLogs(filters = {}) {
    const params = {}
    
    if (filters.action) params.action = filters.action
    if (filters.severity) params.severity = filters.severity
    if (filters.startDate) params.startDate = filters.startDate
    if (filters.endDate) params.endDate = filters.endDate
    
    params.page = filters.page || 1
    params.pageSize = filters.pageSize || 10

    const response = await api.get('/api/AuditLogs/my-logs', { params })
    return response.data
  },

  /**
   * Get audit log by ID
   */
  async getById(id) {
    const response = await api.get(`/api/AuditLogs/${id}`)
    return response.data
  },

  /**
   * Format date for display
   */
  formatDate(dateString) {
    if (!dateString) return '-'
    const date = new Date(dateString)
    return date.toLocaleString()
  },

  /**
   * Get color for severity badge
   */
  getSeverityColor(severity) {
    switch (severity?.toLowerCase()) {
      case 'error':
        return 'error'
      case 'warning':
        return 'warning'
      case 'info':
      default:
        return 'info'
    }
  },

  /**
   * Get icon for action
   */
  getActionIcon(action) {
    if (action.includes('Login')) return 'mdi-login'
    if (action.includes('Logout')) return 'mdi-logout'
    if (action.includes('Created')) return 'mdi-plus'
    if (action.includes('Updated')) return 'mdi-pencil'
    if (action.includes('Deleted')) return 'mdi-delete'
    if (action.includes('Activated')) return 'mdi-check'
    if (action.includes('Deactivated')) return 'mdi-close'
    return 'mdi-information'
  }
}
