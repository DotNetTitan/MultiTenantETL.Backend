import api from './api'
import { API_ENDPOINTS } from '@/config/api'
import { getCurrentUser } from '@/utils/jwtHelper'
import { getOAuthConfig } from '@/config/constants'
import { 
  generateCodeVerifier, 
  generateCodeChallenge, 
  generateState, 
  storePKCEParams, 
  retrievePKCEParams,
  clearPKCEParams
} from '@/utils/pkce'

const API_BASE = import.meta.env.VITE_API_BASE_URL || 'http://localhost:5000'

/**
 * Authentication Service
 * Handles OAuth 2.0 Authorization Code Flow with PKCE
 * 
 * Flow:
 * 1. User clicks login -> initiateLogin() redirects to /connect/authorize
 * 2. Backend redirects to its login page at /auth/login
 * 3. User enters credentials on backend login page
 * 4. Backend authenticates and redirects back to /connect/authorize
 * 5. OpenIddict issues authorization code, redirects to SPA /auth/callback
 * 6. SPA exchanges code for tokens using PKCE code_verifier
 */
export const authService = {
  /**
   * Initiate OAuth 2.0 Authorization Code Flow with PKCE
   * This redirects the browser to the authorization endpoint
   * The backend will then redirect to its login page if not authenticated
   */
  async initiateLogin() {
    // Generate PKCE parameters
    const codeVerifier = generateCodeVerifier()
    const codeChallenge = await generateCodeChallenge(codeVerifier)
    const state = generateState()

    // Store PKCE parameters for the callback (code_verifier is secret, never sent to server)
    storePKCEParams(state, codeVerifier)

    // Get OAuth config from constants
    const oauthConfig = getOAuthConfig()
    const scopes = oauthConfig.scopes.join(' ')

    // Build authorization URL - NO credentials in URL (proper OAuth flow)
    const authParams = new URLSearchParams({
      client_id: oauthConfig.clientId,
      response_type: 'code',
      scope: scopes,
      redirect_uri: `${window.location.origin}/auth/callback`,
      state: state,
      code_challenge: codeChallenge,
      code_challenge_method: 'S256'
    })

    // Redirect to authorization endpoint
    // Backend will redirect to login page if user is not authenticated
    window.location.href = `${API_BASE}${oauthConfig.authorizeEndpoint}?${authParams.toString()}`
  },

  /**
   * Handle OAuth callback - exchange authorization code for tokens
   * Called from the callback page after redirect
   * @param {string} code - Authorization code from URL
   * @param {string} state - State parameter from URL
   * @returns {Promise<{user: Object, accessToken: string, refreshToken: string, expiresIn: number}>}
   */
  async handleCallback(code, state) {
    // Retrieve and validate PKCE parameters
    const { state: storedState, codeVerifier } = retrievePKCEParams()

    if (!storedState || storedState !== state) {
      clearPKCEParams()
      throw new Error('State mismatch - possible CSRF attack')
    }

    if (!codeVerifier) {
      clearPKCEParams()
      throw new Error('Code verifier not found')
    }

    try {
      // Get OAuth config from constants
      const oauthConfig = getOAuthConfig()

      // Exchange authorization code for tokens
      // Note: Authorization codes are single-use per OAuth 2.0 spec
      // If this fails with "invalid_grant", the code was already used
      const tokenParams = new URLSearchParams({
        client_id: oauthConfig.clientId,
        grant_type: 'authorization_code',
        code: code,
        redirect_uri: `${window.location.origin}/auth/callback`,
        code_verifier: codeVerifier
      })

      const tokenResponse = await fetch(`${API_BASE}${oauthConfig.tokenEndpoint}`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/x-www-form-urlencoded'
        },
        body: tokenParams
      })

      if (!tokenResponse.ok) {
        let errorData
        const contentType = tokenResponse.headers.get('content-type')
        
        try {
          if (contentType && contentType.includes('application/json')) {
            errorData = await tokenResponse.json()
          } else {
            // Non-JSON response (HTML error page, plain text, etc.)
            const text = await tokenResponse.text()
            errorData = {
              error: 'server_error',
              error_description: `Server returned ${tokenResponse.status}: ${tokenResponse.statusText}`
            }
          }
        } catch (parseError) {
          errorData = {
            error: 'parse_error',
            error_description: 'Failed to parse server error response'
          }
        }
        
        // If code already used and we have valid tokens, consider it success
        // This handles dev environment double-execution gracefully
        if (errorData.error === 'invalid_grant' && this.isAuthenticated()) {
          const user = getCurrentUser()
          return {
            user,
            accessToken: this.getAccessToken(),
            refreshToken: this.getRefreshToken(),
            idToken: this.getIdToken(),
            expiresIn: 900, // 15 minutes
            tokenType: 'Bearer'
          }
        }

        throw {
          response: { status: tokenResponse.status, data: errorData },
          oauthError: errorData.error,
          message: errorData.error_description || errorData.error || 'Token exchange failed'
        }
      }

      const data = await tokenResponse.json()
      const { access_token, refresh_token, id_token, expires_in, token_type } = data

      // Store tokens
      this.setTokens(access_token, refresh_token, id_token)

      // Clean up session storage after successful exchange
      clearPKCEParams()
      sessionStorage.removeItem('login_credentials')

      // Decode id_token to get user information
      const user = getCurrentUser()

      return {
        user,
        accessToken: access_token,
        refreshToken: refresh_token,
        idToken: id_token,
        expiresIn: expires_in,
        tokenType: token_type || 'Bearer'
      }
    } catch (error) {
      // Clear PKCE params on error to prevent reuse attempts
      clearPKCEParams()
      throw error
    }
  },

  /**
   * Legacy login method for backward compatibility
   * @deprecated Use initiateLogin instead
   */
  async login(credentials) {
    return this.initiateLogin(credentials)
  },

  /**
   * Refresh access token using refresh token
   * @returns {Promise<{user: Object, accessToken: string, refreshToken: string, idToken: string, expiresIn: number, tokenType: string}>}
   */
  async refreshToken() {
    const refreshToken = this.getRefreshToken()
    if (!refreshToken) {
      throw new Error('No refresh token available')
    }

    // Get OAuth config from constants
    const oauthConfig = getOAuthConfig()
    const scopes = oauthConfig.scopes.join(' ')

    const params = new URLSearchParams({
      client_id: oauthConfig.clientId,
      grant_type: 'refresh_token',
      refresh_token: refreshToken,
      scope: scopes
    })

    const response = await fetch(`${API_BASE}${oauthConfig.tokenEndpoint}`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/x-www-form-urlencoded'
      },
      body: params
    })

    if (!response.ok) {
      throw new Error('Token refresh failed')
    }

    const data = await response.json()
    const { access_token, refresh_token, id_token, expires_in, token_type } = data

    this.setTokens(access_token, refresh_token, id_token)

    // Decode id_token to get updated user information
    const user = getCurrentUser()

    return {
      user,
      accessToken: access_token,
      refreshToken: refresh_token,
      idToken: id_token,
      expiresIn: expires_in,
      tokenType: token_type || 'Bearer'
    }
  },

  /**
   * Logout - revoke tokens and clear session
   */
  async logout() {
    try {
      const accessToken = this.getAccessToken()
      const refreshToken = this.getRefreshToken()

      // Call backend logout endpoint
      if (accessToken) {
        await api.post(API_ENDPOINTS.auth.logout)
      }

      // Revoke refresh token if exists
      if (refreshToken) {
        // Get OAuth config from constants
        const oauthConfig = getOAuthConfig()

        const params = new URLSearchParams({
          token: refreshToken,
          token_type_hint: 'refresh_token',
          client_id: oauthConfig.clientId
        })

        await fetch(`${API_BASE}${oauthConfig.revokeEndpoint}`, {
          method: 'POST',
          headers: {
            'Content-Type': 'application/x-www-form-urlencoded'
          },
          body: params
        })
      }
    } catch (error) {
      // Don't throw - logout should always succeed locally
    } finally {
      this.clearTokens()
    }
  },

  /**
   * Register a new user
   * @param {Object} userData - { email, password, firstName, lastName }
   * @returns {Promise<Object>}
   */
  async register(userData) {
    const response = await api.post(API_ENDPOINTS.auth.register, {
      email: userData.email,
      password: userData.password,
      firstName: userData.firstName,
      lastName: userData.lastName
    })
    return response.data
  },

  /**
   * Confirm email address
   * @param {string} userId - User ID (UUID)
   * @param {string} token - Email confirmation token
   * @returns {Promise<Object>}
   * */
  async confirmEmail(userId, token) {
    const response = await api.post(API_ENDPOINTS.auth.confirmEmail, {
      userId,
      token
    })
    return response.data
  },

  /**
   * Request password reset email
   * @param {string} email - User's email address
   * @returns {Promise<Object>}
   */
  async forgotPassword(email) {
    const response = await api.post(API_ENDPOINTS.auth.forgotPassword, {
      email
    })
    return response.data
  },

  /**
   * Reset password with token
   * @param {string} userId - User ID (UUID)
   * @param {string} token - Password reset token
   * @param {string} newPassword - New password
   * @returns {Promise<Object>}
   */
  async resetPassword(userId, token, newPassword) {
    const response = await api.post(API_ENDPOINTS.auth.resetPassword, {
      userId,
      token,
      newPassword
    })
    return response.data
  },

  /**
   * Change password for authenticated user
   * @param {string} currentPassword - Current password
   * @param {string} newPassword - New password
   * @param {string} confirmPassword - Confirmation of new password
   * @returns {Promise<Object>}
   */
  async changePassword(currentPassword, newPassword, confirmPassword) {
    const response = await api.post(API_ENDPOINTS.auth.changePassword, {
      currentPassword,
      newPassword,
      confirmPassword
    })
    return response.data
  },

  /**
   * Switch tenant for multi-tenant support
   * @param {string} tenantId - Tenant ID (UUID)
   * @returns {Promise<{user: Object, accessToken: string, refreshToken: string, idToken: string}>}
   */
  async switchTenant(tenantId) {
    const response = await api.post(API_ENDPOINTS.auth.switchTenant, { tenantId })

    // Backend updates the database and tells us to refresh token
    if (response.data.requiresTokenRefresh) {
      // Refresh token to get new tokens with updated tenant claims
      const tokenResponse = await this.refreshToken()
      
      return {
        user: tokenResponse.user,
        accessToken: tokenResponse.accessToken,
        refreshToken: tokenResponse.refreshToken,
        idToken: tokenResponse.idToken,
        expiresIn: tokenResponse.expiresIn,
        tokenType: tokenResponse.tokenType,
        tenantName: response.data.tenantName
      }
    }

    // Fallback (shouldn't reach here)
    const user = getCurrentUser()
    return {
      user,
      accessToken: this.getAccessToken(),
      refreshToken: this.getRefreshToken(),
      idToken: this.getIdToken(),
      tenantName: response.data.tenantName
    }
  },

  /**
   * Check if user is authenticated (token exists and not expired)
   * @returns {boolean}
   */
  isAuthenticated() {
    const token = this.getIdToken()
    if (!token) return false

    try {
      const user = getCurrentUser()
      return user && user.exp * 1000 > Date.now()
    } catch {
      return false
    }
  },

  // Token storage helpers
  setTokens(accessToken, refreshToken, idToken) {
    if (accessToken) {
      localStorage.setItem('access_token', accessToken)
    }
    if (refreshToken) {
      localStorage.setItem('refresh_token', refreshToken)
    }
    if (idToken) {
      localStorage.setItem('id_token', idToken)
    }
  },

  getAccessToken() {
    return localStorage.getItem('access_token')
  },

  getRefreshToken() {
    return localStorage.getItem('refresh_token')
  },

  getIdToken() {
    return localStorage.getItem('id_token')
  },

  clearTokens() {
    localStorage.removeItem('access_token')
    localStorage.removeItem('refresh_token')
    localStorage.removeItem('id_token')
  }
}