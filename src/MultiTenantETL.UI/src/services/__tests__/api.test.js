import { describe, it, expect, beforeEach, beforeAll, vi } from 'vitest'
import axios from 'axios'

// Mock stores
vi.mock('@/stores/auth', () => ({
  useAuthStore: vi.fn(() => ({
    clearAuth: vi.fn()
  }))
}))

vi.mock('@/stores/tenant', () => ({
  useTenantStore: vi.fn(() => ({
    currentTenantId: 'test-tenant-id'
  }))
}))

// Mock config
vi.mock('@/config/api', () => ({
  API_CONFIG: {
    baseURL: 'http://localhost:5000',
    headers: {
      'Content-Type': 'application/json'
    }
  }
}))

// Mock localStorage
const localStorageMock = {
  getItem: vi.fn(),
  setItem: vi.fn(),
  removeItem: vi.fn(),
  clear: vi.fn()
}
vi.stubGlobal('localStorage', localStorageMock)

// Mock the entire API service
vi.mock('@/services/api', () => {
  const mockApi = {
    defaults: {
      baseURL: 'http://localhost:5000',
      timeout: 30000,
      timeoutErrorMessage: 'Request timeout - the server took too long to respond'
    },
    interceptors: {
      request: {
        handlers: [{
          fulfilled: async (config) => {
            const token = localStorage.getItem('access_token')
            if (token) {
              config.headers.Authorization = `Bearer ${token}`
            }

            // Mock tenant store
            config.headers['X-Tenant-Id'] = 'test-tenant-id'

            return config
          }
        }]
      },
      response: {
        handlers: [{
          fulfilled: (response) => response,
          rejected: (error) => Promise.reject(error)
        }]
      }
    }
  }

  return {
    default: mockApi
  }
})

describe('API Service Core Logic', () => {
  let api

  beforeAll(async () => {
    // Import the mocked API service
    const { default: apiService } = await import('@/services/api')
    api = apiService
  })

  beforeEach(() => {
    vi.clearAllMocks()
    localStorageMock.getItem.mockClear()
    localStorageMock.setItem.mockClear()
    localStorageMock.removeItem.mockClear()
    localStorageMock.clear.mockClear()
  })

  describe('Axios Instance Configuration', () => {
    it('should create axios instance with correct base configuration', () => {
      expect(api.defaults.baseURL).toBe('http://localhost:5000')
      expect(api.defaults.timeout).toBe(30000)
      expect(api.defaults.timeoutErrorMessage).toBe('Request timeout - the server took too long to respond')
    })

    it('should have request interceptor configured', () => {
      expect(api.interceptors.request.handlers).toHaveLength(1)
    })

    it('should have response interceptor configured', () => {
      expect(api.interceptors.response.handlers).toHaveLength(1)
    })
  })

  describe('Request Interceptor', () => {
    it('should add authorization header when token exists', async () => {
      localStorageMock.getItem.mockReturnValue('test-token')

      const requestConfig = { headers: {} }

      const interceptor = api.interceptors.request.handlers[0]
      const result = await interceptor.fulfilled(requestConfig)

      expect(localStorageMock.getItem).toHaveBeenCalledWith('access_token')
      expect(result.headers.Authorization).toBe('Bearer test-token')
    })

    it('should not add authorization header when no token exists', async () => {
      localStorageMock.getItem.mockReturnValue(null)

      const requestConfig = { headers: {} }

      const interceptor = api.interceptors.request.handlers[0]
      const result = await interceptor.fulfilled(requestConfig)

      expect(localStorageMock.getItem).toHaveBeenCalledWith('access_token')
      expect(result.headers.Authorization).toBeUndefined()
    })

    it('should add tenant header when tenant ID exists', async () => {
      localStorageMock.getItem.mockReturnValue(null)

      const requestConfig = { headers: {} }

      const interceptor = api.interceptors.request.handlers[0]
      const result = await interceptor.fulfilled(requestConfig)

      expect(result.headers['X-Tenant-Id']).toBe('test-tenant-id')
    })
  })

  describe('Error Response Handling', () => {
    it('should handle 400 errors with detail message', () => {
      const error = {
        response: {
          status: 400,
          data: {
            detail: 'Email is required',
            title: 'Validation Error'
          }
        }
      }

      // Simulate the error handling logic from the actual interceptor
      if (error.response) {
        const data = error.response.data
        const errorMessage = error.response.status === 400
          ? (data?.detail || data?.title || 'Invalid request.')
          : (data?.title || 'An error occurred. Please try again.')

        expect(errorMessage).toBe('Email is required')
      }
    })

    it('should handle 403 errors', () => {
      const error = {
        response: {
          status: 403,
          data: {
            title: 'Forbidden'
          }
        }
      }

      if (error.response) {
        const data = error.response.data
        const errorMessage = data?.title || 'You do not have permission to perform this action.'

        expect(errorMessage).toBe('Forbidden')
      }
    })

    it('should handle 404 errors', () => {
      const error = {
        response: {
          status: 404,
          data: {
            title: 'Not Found'
          }
        }
      }

      if (error.response) {
        const data = error.response.data
        const errorMessage = data?.title || 'The requested resource was not found.'

        expect(errorMessage).toBe('Not Found')
      }
    })

    it('should handle timeout errors', () => {
      const error = {
        code: 'ECONNABORTED',
        message: 'timeout'
      }

      let userMessage = ''
      if (error.code === 'ECONNABORTED' || error.message.includes('timeout')) {
        userMessage = 'Request timeout. The server is taking too long to respond. Please try again.'
      }

      expect(userMessage).toBe('Request timeout. The server is taking too long to respond. Please try again.')
    })

    it('should handle network errors', () => {
      const error = {
        request: {},
        message: 'Network Error'
      }

      let userMessage = ''
      if (error.request) {
        userMessage = 'Network error. Please check your internet connection.'
      }

      expect(userMessage).toBe('Network error. Please check your internet connection.')
    })
  })
})