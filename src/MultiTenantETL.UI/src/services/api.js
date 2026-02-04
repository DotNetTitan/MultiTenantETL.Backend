import axios from 'axios'
import { API_CONFIG } from '@/config/api'
import { useAuthStore } from '@/stores/auth'
import { useTenantStore } from '@/stores/tenant'

// Create axios instance with configuration
const api = axios.create({
  ...API_CONFIG,
  timeout: 30000, // 30 second timeout for all requests
  timeoutErrorMessage: 'Request timeout - the server took too long to respond'
})

// Request interceptor - add auth token and tenant header
api.interceptors.request.use(
  (config) => {
    const authStore = useAuthStore()
    const tenantStore = useTenantStore()

    // Add auth token if it exists
    const token = localStorage.getItem('access_token')
    if (token) {
      config.headers.Authorization = `Bearer ${token}`
    }

    // Add tenant ID if it exists
    if (tenantStore.currentTenantId) {
      config.headers['X-Tenant-Id'] = tenantStore.currentTenantId
    }

    return config
  },
  (error) => {
    return Promise.reject(error)
  }
)

// Response interceptor - handle token refresh on 401
api.interceptors.response.use(
  (response) => response,
  async (error) => {
    const originalRequest = error.config

    // Prevent infinite retry loops
    if (originalRequest._retryCount >= 3) {
      error.userMessage = 'Maximum retry attempts reached. Please try again later.'
      return Promise.reject(error)
    }

    // If 401 and not already retried, try to refresh token
    if (error.response?.status === 401 && !originalRequest._retry) {
      originalRequest._retry = true

      // Check if we have a refresh token before attempting refresh
      const refreshToken = localStorage.getItem('refresh_token')
      if (!refreshToken) {
        // No refresh token - user is logged out, silently reject without error
        // The router will handle the redirect
        return Promise.reject({ 
          silent: true, 
          message: 'No authentication token' 
        })
      }

      try {
        // Import auth service dynamically to avoid circular dependency
        const { authService } = await import('./authService')

        // Attempt token refresh
        await authService.refreshToken()

        // Retry original request with new token
        const newToken = localStorage.getItem('access_token')
        if (newToken) {
          originalRequest.headers.Authorization = `Bearer ${newToken}`
        }

        return api(originalRequest)
      } catch (refreshError) {
        // Refresh failed - logout and redirect to login
        const authStore = useAuthStore()
        authStore.clearAuth()
        
        // Use router instead of window.location for proper SPA navigation
        if (window.location.pathname !== '/login') {
          window.location.href = '/login'
        }
        
        return Promise.reject({ 
          silent: true, 
          message: 'Token refresh failed' 
        })
      }
    }

    // Handle timeout errors
    if (error.code === 'ECONNABORTED' || error.message.includes('timeout')) {
      error.userMessage = 'Request timeout. The server is taking too long to respond. Please try again.'
      error.isTimeout = true
      return Promise.reject(error)
    }

    // Implement exponential backoff for 5xx errors (but only retry safe methods)
    const isRetryableError = error.response?.status >= 500 && error.response?.status < 600
    const isSafeMethod = ['GET', 'HEAD', 'OPTIONS'].includes(originalRequest.method?.toUpperCase())
    
    if (isRetryableError && isSafeMethod && !originalRequest._retryCount) {
      originalRequest._retryCount = (originalRequest._retryCount || 0) + 1
      
      if (originalRequest._retryCount <= 2) {
        // Exponential backoff: 1s, 2s
        const delay = Math.pow(2, originalRequest._retryCount - 1) * 1000
        
        await new Promise(resolve => setTimeout(resolve, delay))
        return api(originalRequest)
      }
    }

    // Handle different error scenarios (if not already handled above)
    if (!error.userMessage) {
      if (error.response) {
        // Backend uses ProblemDetails format: { status, title, detail, instance, type, traceId }
        const data = error.response.data
        // For 400 errors, use detail (contains specific validation message) if available
        // For other errors, use title (generic category)
        const errorMessage = error.response.status === 400 
          ? (data?.detail || data?.title || 'Invalid request.')
          : (data?.title || 'An error occurred. Please try again.')
        
        // Server responded with error status
        switch (error.response.status) {
          case 400:
            error.userMessage = errorMessage
            break
          case 403:
            error.userMessage = errorMessage || 'You do not have permission to perform this action.'
            break
          case 404:
            error.userMessage = errorMessage || 'The requested resource was not found.'
            break
          case 409:
            error.userMessage = errorMessage
            break
          case 422:
            error.userMessage = errorMessage
            break
          case 429:
            error.userMessage = 'Too many requests. Please try again later.'
            break
          case 500:
          case 502:
          case 503:
          case 504:
            error.userMessage = 'A server error occurred. Please try again later.'
            break
          default:
            error.userMessage = errorMessage
        }
      } else if (error.request) {
        // Request made but no response received (network error)
        error.userMessage = 'Network error. Please check your internet connection.'
        error.isNetworkError = true
      } else {
        // Something else happened
        error.userMessage = 'An unexpected error occurred.'
      }
    }

    return Promise.reject(error)
  }
)

export default api