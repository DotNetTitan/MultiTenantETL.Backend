/**
 * PKCE (Proof Key for Code Exchange) Utilities
 * Implements RFC 7636 for OAuth 2.0 Authorization Code Flow
 */

/**
 * Generate a cryptographically random code verifier
 * @returns {string} Base64URL-encoded random string (43-128 characters)
 */
export function generateCodeVerifier() {
  const array = new Uint8Array(32)
  crypto.getRandomValues(array)
  return base64URLEncode(array)
}

/**
 * Generate code challenge from code verifier using SHA-256
 * @param {string} verifier - Code verifier
 * @returns {Promise<string>} Base64URL-encoded SHA-256 hash
 */
export async function generateCodeChallenge(verifier) {
  const encoder = new TextEncoder()
  const data = encoder.encode(verifier)
  const hash = await crypto.subtle.digest('SHA-256', data)
  return base64URLEncode(new Uint8Array(hash))
}

/**
 * Generate a random state parameter for CSRF protection
 * @returns {string} Random state string
 */
export function generateState() {
  const array = new Uint8Array(16)
  crypto.getRandomValues(array)
  return base64URLEncode(array)
}

/**
 * Base64URL encode (without padding)
 * @param {Uint8Array} buffer - Buffer to encode
 * @returns {string} Base64URL-encoded string
 */
function base64URLEncode(buffer) {
  const base64 = btoa(String.fromCharCode(...buffer))
  return base64
    .replace(/\+/g, '-')
    .replace(/\//g, '_')
    .replace(/=/g, '')
}

/**
 * Store PKCE parameters in session storage
 * @param {string} state - State parameter
 * @param {string} codeVerifier - Code verifier
 */
export function storePKCEParams(state, codeVerifier) {
  sessionStorage.setItem('pkce_state', state)
  sessionStorage.setItem('pkce_code_verifier', codeVerifier)
}

/**
 * Retrieve PKCE parameters from session storage
 * @returns {{state: string|null, codeVerifier: string|null}}
 */
export function retrievePKCEParams() {
  const state = sessionStorage.getItem('pkce_state')
  const codeVerifier = sessionStorage.getItem('pkce_code_verifier')
  
  return { state, codeVerifier }
}

/**
 * Clear PKCE parameters from session storage
 * Call this after successfully exchanging the authorization code
 */
export function clearPKCEParams() {
  sessionStorage.removeItem('pkce_state')
  sessionStorage.removeItem('pkce_code_verifier')
}
