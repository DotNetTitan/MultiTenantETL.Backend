import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import router from '@/router'
import { authService } from '@/services/authService'
import { getCurrentUser, isAdmin as checkIsAdmin } from '@/utils/jwtHelper'

export const useAuthStore = defineStore('auth', () => {
  // State
  const user = ref(null)
  const error = ref(null)
  const loading = ref(false)
  const apiOffline = ref(false)
  const loggingOut = ref(false)

  // Computed properties
  const isAuthenticated = computed(() => !!user.value && !loggingOut.value)
  const isAdmin = computed(() => {
    if (!user.value) return false
    return user.value.role === 'SuperAdmin' || user.value.role === 'TenantAdmin'
  })
  const token = computed(() => authService.getAccessToken()) // For backward compatibility

  /**
   * Initialize - load user from token if available
   */
  function initialize() {
    if (authService.isAuthenticated()) {
      user.value = getCurrentUser()
      
      // Sync tenant ID to localStorage for tenant store initialization
      if (user.value?.tenantId) {
        localStorage.setItem('currentTenantId', user.value.tenantId)
      }
    }
  }

  /**
   * Login - initiates OAuth Authorization Code Flow with PKCE
   * The browser will redirect to the authorization endpoint, then to backend login page
   * After successful login, backend redirects back with authorization code
   */
  async function login() {
    try {
      loading.value = true
      error.value = null
      apiOffline.value = false

      // Initiate OAuth flow - this will redirect the browser to /connect/authorize
      // Backend will then redirect to its login page at /auth/login
      // After successful authentication, user is redirected back to SPA /auth/callback
      await authService.initiateLogin()
      
      // This code won't execute because browser redirects
      // The callback page will handle setting the user
    } catch (err) {
      console.error('Login error:', err)

      if (err.isNetworkError || err.code === 'ERR_NETWORK' || err.code === 'ERR_CONNECTION_REFUSED') {
        apiOffline.value = true
        error.value = 'Cannot connect to the server. Please ensure the API server is running.'
      } else {
        error.value = err.message || 'Login failed. Please try again later.'
      }
      loading.value = false
      throw err
    }
    // Don't set loading.value = false here because browser redirects
  }

  /**
   * Register new user
   */
  async function register(userData) {
    try {
      loading.value = true
      error.value = null

      await authService.register(userData)
      return true
    } catch (err) {
      console.error('Registration error:', err)
      error.value = err.response?.data?.message || err.userMessage || 'Registration failed. Please try again.'
      throw err
    } finally {
      loading.value = false
    }
  }

  /**
   * Logout
   */
  async function logout() {
    // Set logging out state to make isAuthenticated false, allowing navigation to guest route
    loggingOut.value = true
    
    // Navigate to login page
    await router.push('/login')
    
    // Clear auth state after navigation completes
    clearAuth()
    
    // Reset logging out state
    loggingOut.value = false
    
    // Call backend to revoke tokens in background (don't wait)
    authService.logout().catch(err => {
      // Ignore errors - already logged out locally
      console.warn('Backend logout error (ignored):', err.message)
    })
  }

  /**
   * Forgot password
   */
  async function forgotPassword(email) {
    try {
      loading.value = true
      error.value = null

      await authService.forgotPassword(email)
      return true
    } catch (err) {
      console.error('Forgot password error:', err)
      error.value = err.response?.data?.message || err.userMessage || 'Failed to send reset email.'
      throw err
    } finally {
      loading.value = false
    }
  }

  /**
   * Reset password
   */
  async function resetPassword(userId, token, newPassword) {
    try {
      loading.value = true
      error.value = null

      await authService.resetPassword(userId, token, newPassword)
      return true
    } catch (err) {
      console.error('Reset password error:', err)
      error.value = err.response?.data?.message || err.userMessage || 'Failed to reset password.'
      throw err
    } finally {
      loading.value = false
    }
  }

  /**
   * Change password (authenticated users)
   */
  async function changePassword(currentPassword, newPassword, confirmPassword) {
    try {
      loading.value = true
      error.value = null

      await authService.changePassword(currentPassword, newPassword, confirmPassword)
      return true
    } catch (err) {
      console.error('Change password error:', err)
      error.value = err.response?.data?.message || err.userMessage || 'Failed to change password.'
      throw err
    } finally {
      loading.value = false
    }
  }

  /**
   * Switch tenant
   */
  async function switchTenant(tenantId) {
    try {
      loading.value = true
      error.value = null

      const response = await authService.switchTenant(tenantId)

      // Update user with new token data (includes updated tenant info)
      user.value = response.user

      return true
    } catch (err) {
      console.error('Tenant switch error:', err)
      error.value = 'Failed to switch tenant. Please try again.'
      throw err
    } finally {
      loading.value = false
    }
  }

  /**
   * Clear authentication state
   */
  function clearAuth() {
    user.value = null
    authService.clearTokens()
    // Clear tenant selection when logging out
    localStorage.removeItem('currentTenantId')
  }

  /**
   * Reset UI state (loading, error)
   */
  function resetState() {
    loading.value = false
    error.value = null
    apiOffline.value = false
  }

  /**
   * Legacy methods for backward compatibility
   */
  function setUser(userData) {
    user.value = userData
  }

  function setToken(newToken) {
    // Not used anymore - tokens managed by authService
    console.warn('setToken is deprecated - use authService directly')
  }

  // Initialize on store creation
  initialize()

  return {
    // State
    user,
    error,
    loading,
    apiOffline,
    loggingOut,
    token, // For backward compatibility

    // Computed
    isAuthenticated,
    isAdmin,

    // Actions
    login,
    register,
    logout,
    forgotPassword,
    resetPassword,
    changePassword,
    switchTenant,
    clearAuth,
    resetState,
    initialize,

    // Legacy (backward compatibility)
    setUser,
    setToken
  }
})
