import { describe, it, expect, beforeEach, vi, afterEach } from 'vitest'
import { tenantService } from '@/services/tenantService'

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

describe('Tenant Service', () => {
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
    it('should get all tenants without filters', async () => {
      const mockTenants = [
        { id: 1, name: 'Tenant 1', isActive: true },
        { id: 2, name: 'Tenant 2', isActive: false }
      ]

      const mockResponse = { data: mockTenants }
      api.get.mockResolvedValue(mockResponse)

      const result = await tenantService.getAll()

      expect(api.get).toHaveBeenCalledWith('/api/Tenants', { params: {} })
      expect(result).toEqual(mockTenants)
    })

    it('should get all tenants with search filter', async () => {
      const mockTenants = [{ id: 1, name: 'Test Tenant', isActive: true }]
      const filters = { search: 'test' }

      const mockResponse = { data: mockTenants }
      api.get.mockResolvedValue(mockResponse)

      const result = await tenantService.getAll(filters)

      expect(api.get).toHaveBeenCalledWith('/api/Tenants', {
        params: { search: 'test' }
      })
      expect(result).toEqual(mockTenants)
    })

    it('should get all tenants with active status filter', async () => {
      const mockTenants = [{ id: 1, name: 'Active Tenant', isActive: true }]
      const filters = { status: 'active' }

      const mockResponse = { data: mockTenants }
      api.get.mockResolvedValue(mockResponse)

      const result = await tenantService.getAll(filters)

      expect(api.get).toHaveBeenCalledWith('/api/Tenants', {
        params: { isActive: true }
      })
      expect(result).toEqual(mockTenants)
    })

    it('should get all tenants with inactive status filter', async () => {
      const mockTenants = [{ id: 2, name: 'Inactive Tenant', isActive: false }]
      const filters = { status: 'inactive' }

      const mockResponse = { data: mockTenants }
      api.get.mockResolvedValue(mockResponse)

      const result = await tenantService.getAll(filters)

      expect(api.get).toHaveBeenCalledWith('/api/Tenants', {
        params: { isActive: false }
      })
      expect(result).toEqual(mockTenants)
    })
  })

  describe('getMyTenants', () => {
    it('should get current user tenants', async () => {
      const mockTenants = [
        { id: 1, name: 'My Tenant 1', role: 'Admin' },
        { id: 2, name: 'My Tenant 2', role: 'User' }
      ]

      const mockResponse = { data: mockTenants }
      api.get.mockResolvedValue(mockResponse)

      const result = await tenantService.getMyTenants()

      expect(api.get).toHaveBeenCalledWith('/api/Tenants/my-tenants')
      expect(result).toEqual(mockTenants)
    })
  })

  describe('getById', () => {
    it('should get tenant by ID', async () => {
      const tenantId = 1
      const mockTenant = { id: 1, name: 'Test Tenant', isActive: true }

      const mockResponse = { data: mockTenant }
      api.get.mockResolvedValue(mockResponse)

      const result = await tenantService.getById(tenantId)

      expect(api.get).toHaveBeenCalledWith('/api/Tenants/1')
      expect(result).toEqual(mockTenant)
    })
  })

  describe('create', () => {
    it('should create new tenant', async () => {
      const tenantData = {
        name: 'New Tenant',
        slug: 'new-tenant',
        description: 'A new tenant',
        isActive: true
      }
      const mockCreatedTenant = { id: 3, ...tenantData, createdAt: '2023-01-01T00:00:00Z' }

      const mockResponse = { data: mockCreatedTenant }
      api.post.mockResolvedValue(mockResponse)

      const result = await tenantService.create(tenantData)

      expect(api.post).toHaveBeenCalledWith('/api/Tenants', tenantData)
      expect(result).toEqual(mockCreatedTenant)
    })
  })

  describe('update', () => {
    it('should update tenant', async () => {
      const tenantId = 1
      const tenantData = { name: 'Updated Tenant', isActive: false }
      const mockUpdatedTenant = { id: 1, ...tenantData, updatedAt: '2023-01-02T00:00:00Z' }

      const mockResponse = { data: mockUpdatedTenant }
      api.put.mockResolvedValue(mockResponse)

      const result = await tenantService.update(tenantId, tenantData)

      expect(api.put).toHaveBeenCalledWith('/api/Tenants/1', tenantData)
      expect(result).toEqual(mockUpdatedTenant)
    })
  })

  describe('delete', () => {
    it('should delete tenant', async () => {
      const tenantId = 1

      api.delete.mockResolvedValue()

      const result = await tenantService.delete(tenantId)

      expect(api.delete).toHaveBeenCalledWith('/api/Tenants/1')
      expect(result).toBe(true)
    })
  })

  describe('getTenantUsers', () => {
    it('should get users in tenant', async () => {
      const tenantId = 1
      const mockUsers = [
        { id: 1, email: 'user1@example.com', role: 'Admin' },
        { id: 2, email: 'user2@example.com', role: 'User' }
      ]

      const mockResponse = { data: mockUsers }
      api.get.mockResolvedValue(mockResponse)

      const result = await tenantService.getTenantUsers(tenantId)

      expect(api.get).toHaveBeenCalledWith('/api/Tenants/1/users')
      expect(result).toEqual(mockUsers)
    })
  })

  describe('addUserToTenant', () => {
    it('should add user to tenant with default role', async () => {
      const tenantId = 1
      const userId = 123
      const mockResult = { userId: 123, tenantId: 1, roleCode: 'User' }

      const mockResponse = { data: mockResult }
      api.post.mockResolvedValue(mockResponse)

      const result = await tenantService.addUserToTenant(tenantId, userId)

      expect(api.post).toHaveBeenCalledWith('/api/Tenants/1/users', {
        userId: 123,
        tenantId: 1,
        roleCode: 'User'
      })
      expect(result).toEqual(mockResult)
    })

    it('should add user to tenant with custom role', async () => {
      const tenantId = 1
      const userId = 123
      const roleCode = 'Admin'
      const mockResult = { userId: 123, tenantId: 1, roleCode: 'Admin' }

      const mockResponse = { data: mockResult }
      api.post.mockResolvedValue(mockResponse)

      const result = await tenantService.addUserToTenant(tenantId, userId, roleCode)

      expect(api.post).toHaveBeenCalledWith('/api/Tenants/1/users', {
        userId: 123,
        tenantId: 1,
        roleCode: 'Admin'
      })
      expect(result).toEqual(mockResult)
    })
  })

  describe('removeUserFromTenant', () => {
    it('should remove user from tenant', async () => {
      const tenantId = 1
      const userId = 123

      api.delete.mockResolvedValue()

      const result = await tenantService.removeUserFromTenant(tenantId, userId)

      expect(api.delete).toHaveBeenCalledWith('/api/Tenants/1/users/123')
      expect(result).toBe(true)
    })
  })

  describe('updateUserRole', () => {
    it('should update user role in tenant', async () => {
      const tenantId = 1
      const userId = 123
      const roleCode = 'Admin'
      const mockResult = { userId: 123, tenantId: 1, roleCode: 'Admin' }

      const mockResponse = { data: mockResult }
      api.put.mockResolvedValue(mockResponse)

      const result = await tenantService.updateUserRole(tenantId, userId, roleCode)

      expect(api.put).toHaveBeenCalledWith('/api/Tenants/1/users/123/role', { roleCode: 'Admin' })
      expect(result).toEqual(mockResult)
    })
  })

  describe('formatDate', () => {
    it('should format date string', () => {
      const dateString = '2023-01-01T12:00:00Z'
      const result = tenantService.formatDate(dateString)

      expect(result).toMatch(/\d{1,2}\/\d{1,2}\/\d{4}, \d{1,2}:\d{2}:\d{2} (AM|PM)/)
    })

    it('should return dash for null date', () => {
      const result = tenantService.formatDate(null)
      expect(result).toBe('-')
    })

    it('should return dash for undefined date', () => {
      const result = tenantService.formatDate(undefined)
      expect(result).toBe('-')
    })
  })

  describe('applyFilters', () => {
    const mockTenants = [
      { id: 1, name: 'Alpha Corp', slug: 'alpha', description: 'First tenant', isActive: true, createdAt: '2023-01-01' },
      { id: 2, name: 'Beta LLC', slug: 'beta', description: 'Second tenant', isActive: false, createdAt: '2023-01-02' },
      { id: 3, name: 'Gamma Inc', slug: 'gamma', description: 'Third tenant', isActive: true, createdAt: '2023-01-03' }
    ]

    it('should return all tenants when no filters', () => {
      const result = tenantService.applyFilters(mockTenants)
      expect(result).toEqual(mockTenants)
    })

    it('should filter by active status', () => {
      const result = tenantService.applyFilters(mockTenants, { status: 'active' })
      expect(result).toEqual([
        mockTenants[0],
        mockTenants[2]
      ])
    })

    it('should filter by inactive status', () => {
      const result = tenantService.applyFilters(mockTenants, { status: 'inactive' })
      expect(result).toEqual([mockTenants[1]])
    })

    it('should filter by search term in name', () => {
      const result = tenantService.applyFilters(mockTenants, { search: 'alpha' })
      expect(result).toEqual([mockTenants[0]])
    })

    it('should filter by search term in slug', () => {
      const result = tenantService.applyFilters(mockTenants, { search: 'beta' })
      expect(result).toEqual([mockTenants[1]])
    })

    it('should filter by search term in description', () => {
      const result = tenantService.applyFilters(mockTenants, { search: 'second' })
      expect(result).toEqual([mockTenants[1]])
    })

    it('should sort by name ascending', () => {
      const result = tenantService.applyFilters(mockTenants, { sort: 'name_asc' })
      expect(result).toEqual([mockTenants[0], mockTenants[1], mockTenants[2]])
    })

    it('should sort by name descending', () => {
      const result = tenantService.applyFilters(mockTenants, { sort: 'name_desc' })
      expect(result).toEqual([mockTenants[2], mockTenants[1], mockTenants[0]])
    })

    it('should sort by createdAt ascending', () => {
      const result = tenantService.applyFilters(mockTenants, { sort: 'createdAt_asc' })
      expect(result).toEqual([mockTenants[0], mockTenants[1], mockTenants[2]])
    })

    it('should sort by createdAt descending', () => {
      const result = tenantService.applyFilters(mockTenants, { sort: 'createdAt_desc' })
      expect(result).toEqual([mockTenants[2], mockTenants[1], mockTenants[0]])
    })

    it('should combine filters and sorting', () => {
      const result = tenantService.applyFilters(mockTenants, {
        status: 'active',
        search: 'corp',
        sort: 'name_desc'
      })
      expect(result).toEqual([mockTenants[0]])
    })
  })
})