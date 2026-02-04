import { jwtDecode } from 'jwt-decode'

/**
 * JWT Helper Utilities
 * Based on VUEJS_INTEGRATION.md guidelines with adaptations
 */

/**
 * Get current user from JWT id_token (not access_token - it's encrypted!)
 * @returns {Object|null} User object with id, email, name, role, tenant info, permissions
 */
export function getCurrentUser() {
    // Use id_token (readable JWT) instead of access_token (encrypted JWE)
    const token = localStorage.getItem('id_token')
    if (!token) return null

    try {
        const decoded = jwtDecode(token)

        return {
            id: decoded.sub,
            email: decoded.email,
            name: decoded.name,
            role: Array.isArray(decoded['http://schemas.microsoft.com/ws/2008/06/identity/claims/role'])
                ? decoded['http://schemas.microsoft.com/ws/2008/06/identity/claims/role'][0]
                : decoded['http://schemas.microsoft.com/ws/2008/06/identity/claims/role'],
            tenantId: decoded.tenant_id,
            tenantName: decoded.tenant_name,
            permissions: decoded.permissions ? JSON.parse(decoded.permissions) : [],
            exp: decoded.exp,
            iat: decoded.iat
        }
    } catch (error) {
        console.error('Failed to decode JWT:', error)
        return null
    }
}

/**
 * Get user permissions from JWT
 * @returns {string[]} Array of permission strings
 */
export function getUserPermissions() {
    const user = getCurrentUser()
    return user?.permissions || []
}

/**
 * Check if user has specific permission
 * Supports wildcards: "pipelines.*", "*.read", "*.*"
 * Backend uses dot notation (e.g., "pipelines.create")
 * @param {string} permission - Permission to check (format: "resource.action")
 * @returns {boolean}
 */
export function hasPermission(permission) {
    const permissions = getUserPermissions()

    // Check exact match
    if (permissions.includes(permission)) return true

    // Check wildcards
    const [resource, action] = permission.split('.')
    if (!resource || !action) return false

    // Check resource.* (e.g., "pipelines.*")
    if (permissions.includes(`${resource}.*`)) return true

    // Check *.action (e.g., "*.read")
    if (permissions.includes(`*.${action}`)) return true

    // Check super admin *.*
    if (permissions.includes('*.*')) return true

    return false
}

/**
 * Check if user has specific role
 * @param {string} role - Role name to check
 * @returns {boolean}
 */
export function hasRole(role) {
    const user = getCurrentUser()
    return user?.role === role
}

/**
 * Check if user is SuperAdmin
 * @returns {boolean}
 */
export function isSuperAdmin() {
    // Use import at top of file to avoid circular dependency issues
    // Fallback to hardcoded value if constants not loaded
    const user = getCurrentUser()
    return user?.role === 'SuperAdmin'
}

/**
 * Check if user is admin (SuperAdmin or TenantAdmin)
 * @returns {boolean}
 */
export function isAdmin() {
    // Use import at top of file to avoid circular dependency issues
    // Fallback to hardcoded values if constants not loaded
    const user = getCurrentUser()
    return user?.role === 'SuperAdmin' || user?.role === 'TenantAdmin'
}

/**
 * Check if token is expired
 * @param {string} token - JWT token to check
 * @returns {boolean}
 */
export function isTokenExpired(token) {
    if (!token) return true

    try {
        const decoded = jwtDecode(token)
        return decoded.exp * 1000 < Date.now()
    } catch {
        return true
    }
}

/**
 * Get time until token expiration in milliseconds
 * @param {string} token - JWT token
 * @returns {number} Milliseconds until expiration (negative if expired)
 */
export function getTimeUntilExpiration(token) {
    if (!token) return -1

    try {
        const decoded = jwtDecode(token)
        return (decoded.exp * 1000) - Date.now()
    } catch {
        return -1
    }
}
