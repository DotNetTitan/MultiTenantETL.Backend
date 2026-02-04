import { describe, it, expect, beforeEach, vi } from 'vitest'
import jwtDecode from 'jwt-decode'
import {
  getCurrentUser,
  getUserPermissions,
  hasPermission,
  hasRole,
  isSuperAdmin,
  isAdmin,
  isTokenExpired,
  getTimeUntilExpiration
} from '@/utils/jwtHelper'

// Mock jwt-decode
vi.mock('jwt-decode', () => ({
  default: vi.fn()
}))

describe('jwtHelper', () => {
  beforeEach(() => {
    // Clear localStorage before each test
    localStorage.clear()
    vi.clearAllMocks()
  })

  describe('getCurrentUser', () => {
    it('should return null when no id_token in localStorage', () => {
      const result = getCurrentUser()
      expect(result).toBeNull()
    })

    it('should return null when jwtDecode throws an error', () => {
      localStorage.setItem('id_token', 'invalid-token')
      jwtDecode.mockImplementation(() => {
        throw new Error('Invalid token')
      })

      const consoleSpy = vi.spyOn(console, 'error').mockImplementation(() => {})

      const result = getCurrentUser()
      expect(result).toBeNull()
      expect(consoleSpy).toHaveBeenCalledWith('Failed to decode JWT:', expect.any(Error))

      consoleSpy.mockRestore()
    })

    it('should return user object when valid token exists', () => {
      const mockToken = 'valid-token'
      const mockDecoded = {
        sub: 'user-123',
        email: 'user@example.com',
        name: 'John Doe',
        'http://schemas.microsoft.com/ws/2008/06/identity/claims/role': 'User',
        tenant_id: 'tenant-123',
        tenant_name: 'Test Tenant',
        permissions: '["pipelines.read", "connectors.create"]',
        exp: 1234567890,
        iat: 1234567800
      }

      localStorage.setItem('id_token', mockToken)
      jwtDecode.mockReturnValue(mockDecoded)

      const result = getCurrentUser()

      expect(result).toEqual({
        id: 'user-123',
        email: 'user@example.com',
        name: 'John Doe',
        role: 'User',
        tenantId: 'tenant-123',
        tenantName: 'Test Tenant',
        permissions: ['pipelines.read', 'connectors.create'],
        exp: 1234567890,
        iat: 1234567800
      })
    })

    it('should handle array roles correctly', () => {
      const mockDecoded = {
        sub: 'user-123',
        email: 'user@example.com',
        name: 'John Doe',
        'http://schemas.microsoft.com/ws/2008/06/identity/claims/role': ['Admin', 'User'],
        tenant_id: 'tenant-123',
        tenant_name: 'Test Tenant',
        permissions: '[]',
        exp: 1234567890,
        iat: 1234567800
      }

      localStorage.setItem('id_token', 'valid-token')
      jwtDecode.mockReturnValue(mockDecoded)

      const result = getCurrentUser()

      expect(result.role).toBe('Admin') // Should take first role from array
    })

    it('should handle missing permissions gracefully', () => {
      const mockDecoded = {
        sub: 'user-123',
        email: 'user@example.com',
        name: 'John Doe',
        'http://schemas.microsoft.com/ws/2008/06/identity/claims/role': 'User',
        tenant_id: 'tenant-123',
        tenant_name: 'Test Tenant',
        exp: 1234567890,
        iat: 1234567800
      }

      localStorage.setItem('id_token', 'valid-token')
      jwtDecode.mockReturnValue(mockDecoded)

      const result = getCurrentUser()

      expect(result.permissions).toEqual([])
    })
  })

  describe('getUserPermissions', () => {
    it('should return empty array when no user', () => {
      const result = getUserPermissions()
      expect(result).toEqual([])
    })

    it('should return user permissions when user exists', () => {
      const mockDecoded = {
        sub: 'user-123',
        permissions: '["pipelines.read", "connectors.create"]'
      }

      localStorage.setItem('id_token', 'valid-token')
      jwtDecode.mockReturnValue(mockDecoded)

      const result = getUserPermissions()
      expect(result).toEqual(['pipelines.read', 'connectors.create'])
    })
  })

  describe('hasPermission', () => {
    beforeEach(() => {
      const mockDecoded = {
        sub: 'user-123',
        permissions: '["pipelines.read", "connectors.create", "pipelines.*", "*.delete"]'
      }

      localStorage.setItem('id_token', 'valid-token')
      jwtDecode.mockReturnValue(mockDecoded)
    })

    it('should return true for exact permission match', () => {
      expect(hasPermission('pipelines.read')).toBe(true)
      expect(hasPermission('connectors.create')).toBe(true)
    })

    it('should return false for non-matching permission', () => {
      expect(hasPermission('users.create')).toBe(false)
    })

    it('should return true for resource.* wildcard match', () => {
      expect(hasPermission('pipelines.create')).toBe(true)
      expect(hasPermission('pipelines.update')).toBe(true)
    })

    it('should return true for *.action wildcard match', () => {
      expect(hasPermission('users.delete')).toBe(true)
      expect(hasPermission('connectors.delete')).toBe(true)
    })

    it('should return true for *.* super admin permission', () => {
      const mockDecoded = {
        sub: 'user-123',
        permissions: '["*.*"]'
      }
      jwtDecode.mockReturnValue(mockDecoded)

      expect(hasPermission('anything.anything')).toBe(true)
    })

    it('should return false for invalid permission format', () => {
      expect(hasPermission('invalid')).toBe(false)
      expect(hasPermission('')).toBe(false)
    })

    it('should return false when no permissions', () => {
      const mockDecoded = {
        sub: 'user-123',
        permissions: '[]'
      }
      jwtDecode.mockReturnValue(mockDecoded)

      expect(hasPermission('pipelines.read')).toBe(false)
    })
  })

  describe('hasRole', () => {
    it('should return true when user has matching role', () => {
      const mockDecoded = {
        sub: 'user-123',
        'http://schemas.microsoft.com/ws/2008/06/identity/claims/role': 'Admin'
      }

      localStorage.setItem('id_token', 'valid-token')
      jwtDecode.mockReturnValue(mockDecoded)

      expect(hasRole('Admin')).toBe(true)
    })

    it('should return false when user has different role', () => {
      const mockDecoded = {
        sub: 'user-123',
        'http://schemas.microsoft.com/ws/2008/06/identity/claims/role': 'User'
      }

      localStorage.setItem('id_token', 'valid-token')
      jwtDecode.mockReturnValue(mockDecoded)

      expect(hasRole('Admin')).toBe(false)
    })

    it('should return false when no user', () => {
      expect(hasRole('Admin')).toBe(false)
    })
  })

  describe('isSuperAdmin', () => {
    it('should return true when user is SuperAdmin', () => {
      const mockDecoded = {
        sub: 'user-123',
        'http://schemas.microsoft.com/ws/2008/06/identity/claims/role': 'SuperAdmin'
      }

      localStorage.setItem('id_token', 'valid-token')
      jwtDecode.mockReturnValue(mockDecoded)

      expect(isSuperAdmin()).toBe(true)
    })

    it('should return false when user is not SuperAdmin', () => {
      const mockDecoded = {
        sub: 'user-123',
        'http://schemas.microsoft.com/ws/2008/06/identity/claims/role': 'User'
      }

      localStorage.setItem('id_token', 'valid-token')
      jwtDecode.mockReturnValue(mockDecoded)

      expect(isSuperAdmin()).toBe(false)
    })

    it('should return false when no user', () => {
      expect(isSuperAdmin()).toBe(false)
    })
  })

  describe('isAdmin', () => {
    it('should return true when user is SuperAdmin', () => {
      const mockDecoded = {
        sub: 'user-123',
        'http://schemas.microsoft.com/ws/2008/06/identity/claims/role': 'SuperAdmin'
      }

      localStorage.setItem('id_token', 'valid-token')
      jwtDecode.mockReturnValue(mockDecoded)

      expect(isAdmin()).toBe(true)
    })

    it('should return true when user is TenantAdmin', () => {
      const mockDecoded = {
        sub: 'user-123',
        'http://schemas.microsoft.com/ws/2008/06/identity/claims/role': 'TenantAdmin'
      }

      localStorage.setItem('id_token', 'valid-token')
      jwtDecode.mockReturnValue(mockDecoded)

      expect(isAdmin()).toBe(true)
    })

    it('should return false when user is regular User', () => {
      const mockDecoded = {
        sub: 'user-123',
        'http://schemas.microsoft.com/ws/2008/06/identity/claims/role': 'User'
      }

      localStorage.setItem('id_token', 'valid-token')
      jwtDecode.mockReturnValue(mockDecoded)

      expect(isAdmin()).toBe(false)
    })

    it('should return false when no user', () => {
      expect(isAdmin()).toBe(false)
    })
  })

  describe('isTokenExpired', () => {
    it('should return true when no token provided', () => {
      expect(isTokenExpired(null)).toBe(true)
      expect(isTokenExpired(undefined)).toBe(true)
      expect(isTokenExpired('')).toBe(true)
    })

    it('should return true when token is expired', () => {
      const expiredToken = 'expired-token'
      const pastTime = Math.floor(Date.now() / 1000) - 100 // 100 seconds ago

      jwtDecode.mockReturnValue({ exp: pastTime })

      expect(isTokenExpired(expiredToken)).toBe(true)
    })

    it('should return false when token is not expired', () => {
      const validToken = 'valid-token'
      const futureTime = Math.floor(Date.now() / 1000) + 3600 // 1 hour from now

      jwtDecode.mockReturnValue({ exp: futureTime })

      expect(isTokenExpired(validToken)).toBe(false)
    })

    it('should return true when jwtDecode throws error', () => {
      jwtDecode.mockImplementation(() => {
        throw new Error('Invalid token')
      })

      expect(isTokenExpired('invalid-token')).toBe(true)
    })
  })

  describe('getTimeUntilExpiration', () => {
    it('should return -1 when no token provided', () => {
      expect(getTimeUntilExpiration(null)).toBe(-1)
      expect(getTimeUntilExpiration(undefined)).toBe(-1)
      expect(getTimeUntilExpiration('')).toBe(-1)
    })

    it('should return negative value when token is expired', () => {
      const expiredToken = 'expired-token'
      const pastTime = Math.floor(Date.now() / 1000) - 100 // 100 seconds ago

      jwtDecode.mockReturnValue({ exp: pastTime })

      const result = getTimeUntilExpiration(expiredToken)
      expect(result).toBeLessThan(0)
    })

    it('should return positive value when token is not expired', () => {
      const validToken = 'valid-token'
      const futureTime = Math.floor(Date.now() / 1000) + 3600 // 1 hour from now

      jwtDecode.mockReturnValue({ exp: futureTime })

      const result = getTimeUntilExpiration(validToken)
      expect(result).toBeGreaterThan(0)
      expect(result).toBeLessThanOrEqual(3600000) // Should be around 1 hour in milliseconds
    })

    it('should return -1 when jwtDecode throws error', () => {
      jwtDecode.mockImplementation(() => {
        throw new Error('Invalid token')
      })

      expect(getTimeUntilExpiration('invalid-token')).toBe(-1)
    })
  })
})