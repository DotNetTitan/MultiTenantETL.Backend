import { describe, it, expect, beforeEach, vi, afterEach } from 'vitest'
import { userService } from '@/services/userService'

// Mock the api service
vi.mock('@/services/api', () => ({
  default: {
    get: vi.fn(),
    post: vi.fn(),
    put: vi.fn(),
    delete: vi.fn()
  }
}))

import api from '@/services/api'

describe('User Service', () => {
  beforeEach(() => {
    vi.clearAllMocks()

    // Reset api mocks
    api.get.mockReset()
    api.post.mockReset()
    api.put.mockReset()
    api.delete.mockReset()
  })

  afterEach(() => {
    vi.clearAllTimers()
  })

  describe('getAll', () => {
    it('should get all users without filters', async () => {
      const mockUsers = [
        { id: 1, email: 'user1@example.com', firstName: 'User', lastName: 'One' },
        { id: 2, email: 'user2@example.com', firstName: 'User', lastName: 'Two' }
      ]

      const mockResponse = { data: mockUsers }
      api.get.mockResolvedValue(mockResponse)

      const result = await userService.getAll()

      expect(api.get).toHaveBeenCalledWith('/api/Users', { params: {} })
      expect(result).toEqual(mockUsers)
    })

    it('should get all users with search filter', async () => {
      const mockUsers = [{ id: 1, email: 'john@example.com', firstName: 'John', lastName: 'Doe' }]
      const filters = { search: 'john' }

      const mockResponse = { data: mockUsers }
      api.get.mockResolvedValue(mockResponse)

      const result = await userService.getAll(filters)

      expect(api.get).toHaveBeenCalledWith('/api/Users', {
        params: { search: 'john' }
      })
      expect(result).toEqual(mockUsers)
    })

    it('should get all users with email filter', async () => {
      const mockUsers = [{ id: 1, email: 'test@example.com', firstName: 'Test', lastName: 'User' }]
      const filters = { email: 'test@example.com' }

      const mockResponse = { data: mockUsers }
      api.get.mockResolvedValue(mockResponse)

      const result = await userService.getAll(filters)

      expect(api.get).toHaveBeenCalledWith('/api/Users', {
        params: { email: 'test@example.com' }
      })
      expect(result).toEqual(mockUsers)
    })

    it('should get all users with name filter', async () => {
      const mockUsers = [{ id: 1, email: 'user@example.com', firstName: 'John', lastName: 'Smith' }]
      const filters = { name: 'John' }

      const mockResponse = { data: mockUsers }
      api.get.mockResolvedValue(mockResponse)

      const result = await userService.getAll(filters)

      expect(api.get).toHaveBeenCalledWith('/api/Users', {
        params: { name: 'John' }
      })
      expect(result).toEqual(mockUsers)
    })

    it('should get all users with active status filter', async () => {
      const mockUsers = [{ id: 1, email: 'active@example.com', firstName: 'Active', lastName: 'User', isActive: true }]
      const filters = { status: 'Active' }

      const mockResponse = { data: mockUsers }
      api.get.mockResolvedValue(mockResponse)

      const result = await userService.getAll(filters)

      expect(api.get).toHaveBeenCalledWith('/api/Users', {
        params: { isActive: true }
      })
      expect(result).toEqual(mockUsers)
    })

    it('should get all users with inactive status filter', async () => {
      const mockUsers = [{ id: 2, email: 'inactive@example.com', firstName: 'Inactive', lastName: 'User', isActive: false }]
      const filters = { status: 'Inactive' }

      const mockResponse = { data: mockUsers }
      api.get.mockResolvedValue(mockResponse)

      const result = await userService.getAll(filters)

      expect(api.get).toHaveBeenCalledWith('/api/Users', {
        params: { isActive: false }
      })
      expect(result).toEqual(mockUsers)
    })

    it('should get all users with tenant filter', async () => {
      const mockUsers = [{ id: 1, email: 'tenant@example.com', firstName: 'Tenant', lastName: 'User' }]
      const filters = { tenantId: 1 }

      const mockResponse = { data: mockUsers }
      api.get.mockResolvedValue(mockResponse)

      const result = await userService.getAll(filters)

      expect(api.get).toHaveBeenCalledWith('/api/Users', {
        params: { tenantId: 1 }
      })
      expect(result).toEqual(mockUsers)
    })

    it('should handle paginated response', async () => {
      const mockUsers = [{ id: 1, email: 'user1@example.com' }]
      const mockResponse = { data: { users: mockUsers, total: 1, page: 1 } }
      api.get.mockResolvedValue(mockResponse)

      const result = await userService.getAll()

      expect(result).toEqual(mockUsers)
    })
  })

  describe('getMe', () => {
    it('should get current user profile', async () => {
      const mockUser = {
        id: 1,
        email: 'current@example.com',
        firstName: 'Current',
        lastName: 'User',
        isActive: true
      }

      const mockResponse = { data: mockUser }
      api.get.mockResolvedValue(mockResponse)

      const result = await userService.getMe()

      expect(api.get).toHaveBeenCalledWith('/api/Users/me')
      expect(result).toEqual(mockUser)
    })
  })

  describe('updateMe', () => {
    it('should update current user profile', async () => {
      const userData = { firstName: 'Updated', lastName: 'Name' }
      const mockUpdatedUser = {
        id: 1,
        email: 'current@example.com',
        firstName: 'Updated',
        lastName: 'Name'
      }

      const mockResponse = { data: mockUpdatedUser }
      api.put.mockResolvedValue(mockResponse)

      const result = await userService.updateMe(userData)

      expect(api.put).toHaveBeenCalledWith('/api/Users/me', userData)
      expect(result).toEqual(mockUpdatedUser)
    })
  })

  describe('getById', () => {
    it('should get user by ID', async () => {
      const userId = 1
      const mockUser = { id: 1, email: 'user@example.com', firstName: 'Test', lastName: 'User' }

      const mockResponse = { data: mockUser }
      api.get.mockResolvedValue(mockResponse)

      const result = await userService.getById(userId)

      expect(api.get).toHaveBeenCalledWith('/api/Users/1')
      expect(result).toEqual(mockUser)
    })
  })

  describe('update', () => {
    it('should update user', async () => {
      const userId = 1
      const userData = { firstName: 'Updated', lastName: 'User' }
      const mockUpdatedUser = { id: 1, ...userData, email: 'user@example.com' }

      const mockResponse = { data: mockUpdatedUser }
      api.put.mockResolvedValue(mockResponse)

      const result = await userService.update(userId, userData)

      expect(api.put).toHaveBeenCalledWith('/api/Users/1', userData)
      expect(result).toEqual(mockUpdatedUser)
    })
  })

  describe('delete', () => {
    it('should delete user', async () => {
      const userId = 1

      api.delete.mockResolvedValue()

      const result = await userService.delete(userId)

      expect(api.delete).toHaveBeenCalledWith('/api/Users/1')
      expect(result).toBe(true)
    })
  })

  describe('updateStatus', () => {
    it('should update user status', async () => {
      const userId = 1
      const isActive = false
      const mockResult = { id: 1, isActive: false }

      const mockResponse = { data: mockResult }
      api.put.mockResolvedValue(mockResponse)

      const result = await userService.updateStatus(userId, isActive)

      expect(api.put).toHaveBeenCalledWith('/api/Users/1/status', { isActive: false })
      expect(result).toEqual(mockResult)
    })
  })

  describe('toggleStatus', () => {
    it('should toggle user status from active to inactive', async () => {
      const userId = 1
      const mockUser = { id: 1, isActive: true }
      const mockResult = { id: 1, isActive: false }

      // Mock getById to return active user
      api.get.mockResolvedValueOnce({ data: mockUser })
      // Mock updateStatus to return toggled result
      api.put.mockResolvedValueOnce({ data: mockResult })

      const result = await userService.toggleStatus(userId)

      expect(api.get).toHaveBeenCalledWith('/api/Users/1')
      expect(api.put).toHaveBeenCalledWith('/api/Users/1/status', { isActive: false })
      expect(result).toEqual(mockResult)
    })

    it('should toggle user status from inactive to active', async () => {
      const userId = 2
      const mockUser = { id: 2, isActive: false }
      const mockResult = { id: 2, isActive: true }

      // Mock getById to return inactive user
      api.get.mockResolvedValueOnce({ data: mockUser })
      // Mock updateStatus to return toggled result
      api.put.mockResolvedValueOnce({ data: mockResult })

      const result = await userService.toggleStatus(userId)

      expect(api.get).toHaveBeenCalledWith('/api/Users/2')
      expect(api.put).toHaveBeenCalledWith('/api/Users/2/status', { isActive: true })
      expect(result).toEqual(mockResult)
    })
  })

  describe('assignRole', () => {
    it('should assign role to user', async () => {
      const userId = 1
      const roleName = 'Admin'
      const mockResult = { userId: 1, roleName: 'Admin' }

      const mockResponse = { data: mockResult }
      api.post.mockResolvedValue(mockResponse)

      const result = await userService.assignRole(userId, roleName)

      expect(api.post).toHaveBeenCalledWith('/api/Users/1/roles', { roleName: 'Admin' })
      expect(result).toEqual(mockResult)
    })
  })

  describe('removeRole', () => {
    it('should remove role from user', async () => {
      const userId = 1
      const roleName = 'Admin'

      api.delete.mockResolvedValue()

      const result = await userService.removeRole(userId, roleName)

      expect(api.delete).toHaveBeenCalledWith('/api/Users/1/roles', { data: { roleName: 'Admin' } })
      expect(result).toBe(true)
    })
  })

  describe('getUserTenants', () => {
    it('should get user tenant memberships', async () => {
      const userId = 1
      const mockTenants = [
        { tenantId: 1, tenantName: 'Tenant 1', roleCode: 'Admin' },
        { tenantId: 2, tenantName: 'Tenant 2', roleCode: 'User' }
      ]

      const mockResponse = { data: mockTenants }
      api.get.mockResolvedValue(mockResponse)

      const result = await userService.getUserTenants(userId)

      expect(api.get).toHaveBeenCalledWith('/api/Users/1/tenants')
      expect(result).toEqual(mockTenants)
    })
  })

  describe('addUserToTenant', () => {
    it('should add user to tenant with default role', async () => {
      const userId = 1
      const tenantId = 1
      const mockResult = { userId: 1, tenantId: 1, roleCode: 'User' }

      const mockResponse = { data: mockResult }
      api.post.mockResolvedValue(mockResponse)

      const result = await userService.addUserToTenant(userId, tenantId)

      expect(api.post).toHaveBeenCalledWith('/api/Users/1/tenants', {
        userId: 1,
        tenantId: 1,
        roleCode: 'User'
      })
      expect(result).toEqual(mockResult)
    })

    it('should add user to tenant with custom role', async () => {
      const userId = 1
      const tenantId = 1
      const roleCode = 'Admin'
      const mockResult = { userId: 1, tenantId: 1, roleCode: 'Admin' }

      const mockResponse = { data: mockResult }
      api.post.mockResolvedValue(mockResponse)

      const result = await userService.addUserToTenant(userId, tenantId, roleCode)

      expect(api.post).toHaveBeenCalledWith('/api/Users/1/tenants', {
        userId: 1,
        tenantId: 1,
        roleCode: 'Admin'
      })
      expect(result).toEqual(mockResult)
    })
  })

  describe('removeUserFromTenant', () => {
    it('should remove user from tenant', async () => {
      const userId = 1
      const tenantId = 1

      api.delete.mockResolvedValue()

      const result = await userService.removeUserFromTenant(userId, tenantId)

      expect(api.delete).toHaveBeenCalledWith('/api/Users/1/tenants/1')
      expect(result).toBe(true)
    })
  })

  describe('updateUserTenantRole', () => {
    it('should update user role in tenant', async () => {
      const userId = 1
      const tenantId = 1
      const roleCode = 'Admin'
      const mockResult = { userId: 1, tenantId: 1, roleCode: 'Admin' }

      const mockResponse = { data: mockResult }
      api.put.mockResolvedValue(mockResponse)

      const result = await userService.updateUserTenantRole(userId, tenantId, roleCode)

      expect(api.put).toHaveBeenCalledWith('/api/Users/1/tenants/1/role', { roleCode: 'Admin' })
      expect(result).toEqual(mockResult)
    })
  })

  describe('resetPasswordAdmin', () => {
    it('should reset user password as admin', async () => {
      const userId = 1
      const newPassword = 'newpassword123'
      const mockResult = { message: 'Password reset successfully' }

      const mockResponse = { data: mockResult }
      api.post.mockResolvedValue(mockResponse)

      const result = await userService.resetPasswordAdmin(userId, newPassword)

      expect(api.post).toHaveBeenCalledWith('/api/Users/1/reset-password', { newPassword: 'newpassword123' })
      expect(result).toEqual(mockResult)
    })
  })

  describe('formatDate', () => {
    it('should format date string', () => {
      const dateString = '2023-01-01T12:00:00Z'
      const result = userService.formatDate(dateString)

      expect(result).toMatch(/\d{1,2}\/\d{1,2}\/\d{4}, \d{1,2}:\d{2}:\d{2} (AM|PM)/)
    })

    it('should return dash for null date', () => {
      const result = userService.formatDate(null)
      expect(result).toBe('-')
    })

    it('should return dash for undefined date', () => {
      const result = userService.formatDate(undefined)
      expect(result).toBe('-')
    })
  })

  describe('getRoleColor', () => {
    it('should return red for SuperAdmin', () => {
      expect(userService.getRoleColor('SuperAdmin')).toBe('red')
      expect(userService.getRoleColor('superadmin')).toBe('red')
    })

    it('should return deep-purple for Admin roles', () => {
      expect(userService.getRoleColor('Admin')).toBe('deep-purple')
      expect(userService.getRoleColor('TenantAdmin')).toBe('deep-purple')
      expect(userService.getRoleColor('admin')).toBe('deep-purple')
    })

    it('should return indigo for Manager', () => {
      expect(userService.getRoleColor('Manager')).toBe('indigo')
      expect(userService.getRoleColor('manager')).toBe('indigo')
    })

    it('should return blue for other roles', () => {
      expect(userService.getRoleColor('User')).toBe('blue')
      expect(userService.getRoleColor('unknown')).toBe('blue')
      expect(userService.getRoleColor(null)).toBe('blue')
    })
  })

  describe('createEmpty', () => {
    it('should create empty user object', () => {
      const result = userService.createEmpty()

      expect(result).toEqual({
        id: null,
        firstName: '',
        lastName: '',
        email: '',
        role: 'User',
        isActive: true
      })
    })
  })

  describe('getAvailableRoles', () => {
    it('should return available roles', () => {
      const result = userService.getAvailableRoles()

      expect(result).toEqual(['SuperAdmin', 'TenantAdmin', 'User'])
    })
  })

  describe('applyFilters', () => {
    const mockUsers = [
      { id: 1, firstName: 'John', lastName: 'Doe', email: 'john@example.com', isActive: true, createdAt: '2023-01-01' },
      { id: 2, firstName: 'Jane', lastName: 'Smith', email: 'jane@example.com', isActive: false, createdAt: '2023-01-02' },
      { id: 3, firstName: 'Bob', lastName: 'Johnson', email: 'bob@example.com', isActive: true, createdAt: '2023-01-03' }
    ]

    it('should return all users when no filters', () => {
      const result = userService.applyFilters(mockUsers)
      expect(result).toEqual(mockUsers)
    })

    it('should filter by search in name', () => {
      const result = userService.applyFilters(mockUsers, { search: 'john' })
      expect(result).toEqual([mockUsers[0], mockUsers[2]])
    })

    it('should filter by search in email', () => {
      const result = userService.applyFilters(mockUsers, { search: 'jane' })
      expect(result).toEqual([mockUsers[1]])
    })

    it('should filter by active status', () => {
      const result = userService.applyFilters(mockUsers, { status: 'Active' })
      expect(result).toEqual([mockUsers[0], mockUsers[2]])
    })

    it('should filter by inactive status', () => {
      const result = userService.applyFilters(mockUsers, { status: 'Inactive' })
      expect(result).toEqual([mockUsers[1]])
    })

    it('should sort by name ascending', () => {
      const result = userService.applyFilters(mockUsers, { sort: 'name_asc' })
      expect(result).toEqual([mockUsers[2], mockUsers[1], mockUsers[0]]) // Bob Johnson, Jane Smith, John Doe
    })

    it('should sort by name descending', () => {
      const result = userService.applyFilters(mockUsers, { sort: 'name_desc' })
      expect(result).toEqual([mockUsers[0], mockUsers[1], mockUsers[2]]) // John Doe, Jane Smith, Bob Johnson
    })

    it('should sort by email ascending', () => {
      const result = userService.applyFilters(mockUsers, { sort: 'email_asc' })
      expect(result).toEqual([mockUsers[2], mockUsers[1], mockUsers[0]]) // bob, jane, john
    })

    it('should sort by created ascending', () => {
      const result = userService.applyFilters(mockUsers, { sort: 'created_asc' })
      expect(result).toEqual([mockUsers[0], mockUsers[1], mockUsers[2]])
    })

    it('should sort by created descending', () => {
      const result = userService.applyFilters(mockUsers, { sort: 'created_desc' })
      expect(result).toEqual([mockUsers[2], mockUsers[1], mockUsers[0]])
    })

    it('should combine filters and sorting', () => {
      const result = userService.applyFilters(mockUsers, {
        status: 'Active',
        search: 'john',
        sort: 'name_desc'
      })
      expect(result).toEqual([mockUsers[0], mockUsers[2]]) // John Doe, Bob Johnson (active, matching search, sorted desc)
    })
  })
})