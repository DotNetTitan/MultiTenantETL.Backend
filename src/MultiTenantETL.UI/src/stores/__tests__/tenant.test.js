import { describe, it, expect, beforeEach, vi, afterEach } from 'vitest'
import { setActivePinia, createPinia } from 'pinia'
import { useTenantStore } from '../tenant'
import { tenantService } from '@/services/tenantService'

// Mock dependencies
const mockAuthStore = {
  user: null,
  switchTenant: vi.fn()
}

vi.mock('@/services/tenantService', () => ({
  tenantService: {
    getAll: vi.fn(),
    create: vi.fn()
  }
}))

vi.mock('../auth', () => ({
  useAuthStore: vi.fn(() => mockAuthStore)
}))

vi.mock('@/router', () => ({
  default: {
    currentRoute: {
      value: { path: '/dashboard' }
    },
    push: vi.fn()
  }
}))

// Mock localStorage
const localStorageMock = {
  getItem: vi.fn(() => null), // Default to null
  setItem: vi.fn(),
  removeItem: vi.fn(),
  clear: vi.fn()
}
Object.defineProperty(window, 'localStorage', {
  value: localStorageMock
})

describe('useTenantStore', () => {
  let store

  beforeEach(() => {
    setActivePinia(createPinia())

    // Reset mocks
    vi.clearAllMocks()
    mockAuthStore.user = null
    mockAuthStore.switchTenant.mockClear()

    store = useTenantStore()
  })

  afterEach(() => {
    vi.clearAllMocks()
  })

  describe('initial state', () => {
    it('should initialize with correct default values', () => {
      localStorageMock.getItem.mockReturnValue('tenant-123')

      // Re-create store to test initialization
      store = useTenantStore()

      expect(store.currentTenantId.value).toBe('tenant-123')
      expect(store.tenants.value).toEqual([])
      expect(store.loading.value).toBe(false)
      expect(store.error.value).toBe(null)
    })

    it('should initialize with null currentTenantId when localStorage is empty', () => {
      localStorageMock.getItem.mockReturnValue(null)

      store = useTenantStore()

      expect(store.currentTenantId.value).toBe(null)
    })
  })

  describe('currentTenant computed', () => {
    it('should return null when no currentTenantId', () => {
      store.currentTenantId.value = null
      store.tenants.value = [{ id: 'tenant-1', name: 'Tenant 1' }]

      expect(store.currentTenant.value).toBe(null)
    })

    it('should return the current tenant when found', () => {
      store.currentTenantId.value = 'tenant-1'
      store.tenants.value = [
        { id: 'tenant-1', name: 'Tenant 1' },
        { id: 'tenant-2', name: 'Tenant 2' }
      ]

      expect(store.currentTenant.value).toEqual({ id: 'tenant-1', name: 'Tenant 1' })
    })

    it('should return null when current tenant not found', () => {
      store.currentTenantId.value = 'tenant-3'
      store.tenants.value = [
        { id: 'tenant-1', name: 'Tenant 1' },
        { id: 'tenant-2', name: 'Tenant 2' }
      ]

      expect(store.currentTenant.value).toBe(null)
    })
  })

  describe('fetchTenants', () => {
    it('should fetch tenants successfully', async () => {
      const mockTenants = [
        { id: 'tenant-1', name: 'Tenant 1' },
        { id: 'tenant-2', name: 'Tenant 2' }
      ]
      tenantService.getAll.mockResolvedValue(mockTenants)

      await store.fetchTenants()

      expect(tenantService.getAll).toHaveBeenCalled()
      expect(store.tenants.value).toEqual(mockTenants)
      expect(store.loading.value).toBe(false)
      expect(store.error.value).toBe(null)
    })

    it('should handle network errors during fetch', async () => {
      const networkError = new Error('Network Error')
      networkError.code = 'ERR_CONNECTION_REFUSED'
      tenantService.getAll.mockRejectedValue(networkError)

      await expect(store.fetchTenants()).rejects.toThrow('Network Error')

      expect(store.tenants.value).toEqual([])
      expect(store.loading.value).toBe(false)
      expect(store.error.value).toBe('Unable to connect to the server. Please check if the API server is running.')
    })

    it('should handle authentication errors during fetch', async () => {
      const authError = new Error('Unauthorized')
      authError.response = { status: 401 }
      tenantService.getAll.mockRejectedValue(authError)

      await expect(store.fetchTenants()).rejects.toThrow('Unauthorized')

      expect(store.tenants.value).toEqual([])
      expect(store.loading.value).toBe(false)
      expect(store.error.value).toBe('Authentication required')
    })

    it('should handle other errors during fetch', async () => {
      const otherError = new Error('Server Error')
      tenantService.getAll.mockRejectedValue(otherError)

      await expect(store.fetchTenants()).rejects.toThrow('Server Error')

      expect(store.tenants.value).toEqual([])
      expect(store.loading.value).toBe(false)
      expect(store.error.value).toBe('Failed to load tenants. Please try again later.')
    })

    it('should set loading state correctly', async () => {
      tenantService.getAll.mockResolvedValue([])

      const promise = store.fetchTenants()

      expect(store.loading.value).toBe(true)

      await promise

      expect(store.loading.value).toBe(false)
    })
  })

  describe('createTenant', () => {
    const tenantData = { name: 'New Tenant', description: 'Test tenant' }

    it('should create tenant successfully', async () => {
      const newTenant = { id: 'tenant-3', ...tenantData }
      tenantService.create.mockResolvedValue(newTenant)

      const result = await store.createTenant(tenantData)

      expect(tenantService.create).toHaveBeenCalledWith(tenantData)
      expect(store.tenants.value).toEqual([newTenant])
      expect(result).toEqual(newTenant)
      expect(store.loading.value).toBe(false)
      expect(store.error.value).toBe(null)
    })

    it('should handle network errors during creation', async () => {
      const networkError = new Error('Network Error')
      networkError.code = 'ERR_CONNECTION_REFUSED'
      tenantService.create.mockRejectedValue(networkError)

      await expect(store.createTenant(tenantData)).rejects.toThrow('Network Error')

      expect(store.loading.value).toBe(false)
      expect(store.error.value).toBe('Unable to connect to the server. Please check if the API server is running.')
    })

    it('should handle other errors during creation', async () => {
      const otherError = new Error('Validation Error')
      tenantService.create.mockRejectedValue(otherError)

      await expect(store.createTenant(tenantData)).rejects.toThrow('Validation Error')

      expect(store.loading.value).toBe(false)
      expect(store.error.value).toBe('Failed to create tenant. Please try again later.')
    })

    it('should set loading state correctly', async () => {
      const newTenant = { id: 'tenant-3', ...tenantData }
      tenantService.create.mockResolvedValue(newTenant)

      const promise = store.createTenant(tenantData)

      expect(store.loading.value).toBe(true)

      await promise

      expect(store.loading.value).toBe(false)
    })
  })

  describe('setCurrentTenant', () => {
    const tenantId = 'tenant-1'

    it('should switch tenant successfully', async () => {
      mockAuthStore.switchTenant.mockResolvedValue()
      // Mock the auth store user being updated after switch
      mockAuthStore.user = { tenantId }

      const result = await store.setCurrentTenant(tenantId)

      expect(mockAuthStore.switchTenant).toHaveBeenCalledWith(tenantId)
      expect(store.currentTenantId.value).toBe(tenantId)
      expect(localStorageMock.setItem).toHaveBeenCalledWith('currentTenantId', tenantId)
      expect(result).toBe(true)
      expect(store.loading.value).toBe(false)
      expect(store.error.value).toBe(null)
    })

    it('should navigate to dashboard when not already there', async () => {
      mockAuthStore.switchTenant.mockResolvedValue()
      mockAuthStore.user = { tenantId }

      const router = (await import('@/router')).default
      router.currentRoute.value.path = '/tenants'

      await store.setCurrentTenant(tenantId)

      expect(router.push).toHaveBeenCalledWith('/dashboard')
    })

    it('should not navigate when already on dashboard', async () => {
      mockAuthStore.switchTenant.mockResolvedValue()
      mockAuthStore.user = { tenantId }

      const router = (await import('@/router')).default
      router.currentRoute.value.path = '/dashboard'

      await store.setCurrentTenant(tenantId)

      expect(router.push).not.toHaveBeenCalled()
    })

    it('should handle tenant switch errors', async () => {
      const switchError = new Error('Tenant switch failed')
      mockAuthStore.switchTenant.mockRejectedValue(switchError)

      await expect(store.setCurrentTenant(tenantId)).rejects.toThrow('Tenant switch failed')

      expect(store.loading.value).toBe(false)
      expect(store.error.value).toBe('Failed to switch tenant. Please try again.')
      expect(localStorageMock.setItem).not.toHaveBeenCalled()
    })

    it('should set loading state correctly', async () => {
      mockAuthStore.switchTenant.mockResolvedValue()
      mockAuthStore.user = { tenantId }

      const promise = store.setCurrentTenant(tenantId)

      expect(store.loading.value).toBe(true)

      await promise

      expect(store.loading.value).toBe(false)
    })

    it('should handle null tenantId', async () => {
      mockAuthStore.switchTenant.mockResolvedValue()
      mockAuthStore.user = { tenantId: null }

      await store.setCurrentTenant(null)

      expect(localStorageMock.setItem).toHaveBeenCalledWith('currentTenantId', '')
    })
  })
})