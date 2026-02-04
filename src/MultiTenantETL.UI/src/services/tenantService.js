import api from './api'

export const tenantService = {
  /**
   * Get all tenants (SuperAdmin) or current user's tenants
   */
  async getAll(filters = {}) {
    const params = {}
    
    if (filters.search) {
      params.search = filters.search
    }
    
    if (filters.status && filters.status !== 'all') {
      params.isActive = filters.status === 'active'
    }

    const response = await api.get('/api/Tenants', { params })
    return response.data
  },

  /**
   * Get current user's tenants
   */
  async getMyTenants() {
    const response = await api.get('/api/Tenants/my-tenants')
    return response.data
  },

  /**
   * Get tenant by ID
   */
  async getById(id) {
    const response = await api.get(`/api/Tenants/${id}`)
    return response.data
  },

  /**
   * Create new tenant (SuperAdmin only)
   */
  async create(tenantData) {
    const response = await api.post('/api/Tenants', tenantData)
    return response.data
  },

  /**
   * Update tenant (Admin)
   */
  async update(id, tenantData) {
    const response = await api.put(`/api/Tenants/${id}`, tenantData)
    return response.data
  },

  /**
   * Delete tenant (SuperAdmin only)
   */
  async delete(id) {
    await api.delete(`/api/Tenants/${id}`)
    return true
  },

  /**
   * Get users in tenant
   */
  async getTenantUsers(tenantId) {
    const response = await api.get(`/api/Tenants/${tenantId}/users`)
    return response.data
  },

  /**
   * Add user to tenant
   */
  async addUserToTenant(tenantId, userId, roleCode = 'User') {
    const response = await api.post(`/api/Tenants/${tenantId}/users`, { 
      userId, 
      tenantId, 
      roleCode 
    })
    return response.data
  },

  /**
   * Remove user from tenant
   */
  async removeUserFromTenant(tenantId, userId) {
    await api.delete(`/api/Tenants/${tenantId}/users/${userId}`)
    return true
  },

  /**
   * Update user role in tenant
   */
  async updateUserRole(tenantId, userId, roleCode) {
    const response = await api.put(`/api/Tenants/${tenantId}/users/${userId}/role`, { roleCode })
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
   * Client-side filtering and sorting (for already fetched data)
   */
  applyFilters(tenants, filters = {}) {
    let filtered = [...tenants]
    
    if (filters.status && filters.status !== 'all') {
      const isActive = filters.status === 'active'
      filtered = filtered.filter(tenant => tenant.isActive === isActive)
    }

    if (filters.search) {
      const searchLower = filters.search.toLowerCase()
      filtered = filtered.filter(tenant => 
        tenant.name?.toLowerCase().includes(searchLower) ||
        tenant.slug?.toLowerCase().includes(searchLower) ||
        tenant.description?.toLowerCase().includes(searchLower)
      )
    }
    
    if (filters.sort) {
      const [field, order] = filters.sort.split('_')
      filtered.sort((a, b) => {
        let aVal = a[field]
        let bVal = b[field]
        
        if (field === 'createdAt') {
          aVal = new Date(aVal)
          bVal = new Date(bVal)
        }
        
        if (order === 'desc') {
          return bVal > aVal ? 1 : -1
        }
        return aVal > bVal ? 1 : -1
      })
    }
    
    return filtered
  }
}
