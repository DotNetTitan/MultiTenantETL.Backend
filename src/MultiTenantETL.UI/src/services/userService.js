import api from './api'

export const userService = {
  /**
   * Get all users (Admin only)
   */
  async getAll(filters = {}) {
    const params = {}

    if (filters.search) {
      params.search = filters.search
    }

    if (filters.email) {
      params.email = filters.email
    }

    if (filters.name) {
      params.name = filters.name
    }

    if (filters.status && filters.status !== 'All') {
      params.isActive = filters.status === 'Active'
    }

    if (filters.tenantId) {
      params.tenantId = filters.tenantId
    }

    const response = await api.get('/api/Users', { params })

    // Handle paginated response - extract users array
    if (response.data && response.data.users) {
      return response.data.users
    }

    // Handle direct array response
    return response.data
  },

  /**
   * Get current user profile
   */
  async getMe() {
    const response = await api.get('/api/Users/me')
    return response.data
  },

  /**
   * Update current user profile
   */
  async updateMe(userData) {
    const response = await api.put('/api/Users/me', userData)
    return response.data
  },

  /**
   * Get user by ID (Admin only)
   */
  async getById(id) {
    const response = await api.get(`/api/Users/${id}`)
    return response.data
  },

  /**
   * Update user (SuperAdmin only)
   */
  async update(id, userData) {
    const response = await api.put(`/api/Users/${id}`, userData)
    return response.data
  },

  /**
   * Delete user (SuperAdmin only)
   */
  async delete(id) {
    await api.delete(`/api/Users/${id}`)
    return true
  },

  /**
   * Activate/deactivate user (SuperAdmin only)
   */
  async updateStatus(id, isActive) {
    const response = await api.put(`/api/Users/${id}/status`, { isActive })
    return response.data
  },

  /**
   * Toggle user status
   */
  async toggleStatus(id) {
    // Get current user to toggle status
    const user = await this.getById(id)
    return await this.updateStatus(id, !user.isActive)
  },

  /**
   * Assign role to user (SuperAdmin only)
   */
  async assignRole(id, roleName) {
    const response = await api.post(`/api/Users/${id}/roles`, { roleName })
    return response.data
  },

  /**
   * Remove role from user (SuperAdmin only)
   */
  async removeRole(id, roleName) {
    await api.delete(`/api/Users/${id}/roles`, { data: { roleName } })
    return true
  },

  /**
   * Get user's tenant memberships
   */
  async getUserTenants(id) {
    const response = await api.get(`/api/Users/${id}/tenants`)
    return response.data
  },

  /**
   * Add user to tenant
   */
  async addUserToTenant(userId, tenantId, roleCode = 'User') {
    const response = await api.post(`/api/Users/${userId}/tenants`, {
      userId,
      tenantId,
      roleCode
    })
    return response.data
  },

  /**
   * Remove user from tenant
   */
  async removeUserFromTenant(userId, tenantId) {
    await api.delete(`/api/Users/${userId}/tenants/${tenantId}`)
    return true
  },

  /**
   * Update user's role in tenant
   */
  async updateUserTenantRole(userId, tenantId, roleCode) {
    const response = await api.put(`/api/Users/${userId}/tenants/${tenantId}/role`, { roleCode })
    return response.data
  },

  /**
   * Admin-initiated password reset (SuperAdmin only)
   */
  async resetPasswordAdmin(id, newPassword) {
    const response = await api.post(`/api/Users/${id}/reset-password`, { newPassword })
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
   * Get color for role badge
   */
  getRoleColor(role) {
    switch (role?.toLowerCase()) {
      case 'superadmin':
        return 'red'
      case 'tenantadmin':
      case 'admin':
        return 'deep-purple'
      case 'manager':
        return 'indigo'
      default:
        return 'blue'
    }
  },

  /**
   * Create empty user object
   */
  createEmpty() {
    return {
      id: null,
      firstName: '',
      lastName: '',
      email: '',
      role: 'User',
      isActive: true
    }
  },

  /**
   * Get available roles
   */
  getAvailableRoles() {
    return ['SuperAdmin', 'TenantAdmin', 'User']
  },

  /**
   * Client-side filtering and sorting (for already fetched data)
   */
  applyFilters(users, filters = {}) {
    let filtered = [...users]

    if (filters.search) {
      const searchLower = filters.search.toLowerCase()
      filtered = filtered.filter(user => {
        const name = `${user.firstName} ${user.lastName}`.toLowerCase()
        return name.includes(searchLower) ||
          user.email?.toLowerCase().includes(searchLower)
      })
    }

    if (filters.status && filters.status !== 'All') {
      const isActive = filters.status === 'Active'
      filtered = filtered.filter(user => user.isActive === isActive)
    }

    if (filters.sort) {
      const [field, order] = filters.sort.split('_')
      filtered.sort((a, b) => {
        let aVal, bVal

        if (field === 'name') {
          aVal = `${a.firstName} ${a.lastName}`
          bVal = `${b.firstName} ${b.lastName}`
        } else if (field === 'created') {
          aVal = new Date(a.createdAt)
          bVal = new Date(b.createdAt)
        } else {
          aVal = a[field]
          bVal = b[field]
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
