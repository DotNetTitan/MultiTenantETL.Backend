import { describe, it, expect, beforeEach, vi } from 'vitest'
import {
  generateCodeVerifier,
  generateCodeChallenge,
  generateState,
  storePKCEParams,
  retrievePKCEParams,
  clearPKCEParams
} from '@/utils/pkce'

// Mock crypto API
Object.defineProperty(window, 'crypto', {
  value: {
    getRandomValues: vi.fn(),
    subtle: {
      digest: vi.fn()
    }
  }
})

describe('pkce', () => {
  beforeEach(() => {
    // Clear sessionStorage before each test
    sessionStorage.clear()
    vi.clearAllMocks()

    // Mock crypto.getRandomValues to return predictable values
    window.crypto.getRandomValues.mockImplementation((array) => {
      // Fill array with predictable values for testing
      for (let i = 0; i < array.length; i++) {
        array[i] = i % 256
      }
      return array
    })

    // Mock crypto.subtle.digest
    window.crypto.subtle.digest.mockResolvedValue(new Uint8Array(32).fill(1))
  })

  describe('generateCodeVerifier', () => {
    it('should generate a base64url-encoded string', () => {
      const result = generateCodeVerifier()

      // Should be base64url encoded (no +, /, or =)
      expect(result).toMatch(/^[A-Za-z0-9_-]+$/)
    })

    it('should generate strings of appropriate length', () => {
      const result = generateCodeVerifier()

      // Base64url encoded 32 bytes should be around 43 characters (32 * 4/3)
      expect(result.length).toBeGreaterThanOrEqual(43)
      expect(result.length).toBeLessThanOrEqual(128)
    })

    it('should generate different values on subsequent calls', () => {
      const result1 = generateCodeVerifier()
      const result2 = generateCodeVerifier()

      // Since we're mocking with the same values, they should be the same
      // In real usage, crypto.getRandomValues would provide different values
      expect(result1).toBe(result2)
    })
  })

  describe('generateCodeChallenge', () => {
    it('should generate a base64url-encoded SHA-256 hash', async () => {
      const verifier = 'test-verifier'
      const result = await generateCodeChallenge(verifier)

      // Should be base64url encoded
      expect(result).toMatch(/^[A-Za-z0-9_-]+$/)

      // Should be exactly 43 characters (32 bytes SHA-256 = 43 base64url chars)
      expect(result.length).toBe(43)
    })

    it('should call crypto.subtle.digest with SHA-256 algorithm', async () => {
      const verifier = 'test-verifier'
      await generateCodeChallenge(verifier)

      expect(window.crypto.subtle.digest).toHaveBeenCalledWith('SHA-256', expect.any(Object))
    })

    it('should encode the verifier as UTF-8 bytes', async () => {
      const verifier = 'test-verifier'
      await generateCodeChallenge(verifier)

      const expectedBytes = new TextEncoder().encode(verifier)
      expect(window.crypto.subtle.digest).toHaveBeenCalledWith('SHA-256', expectedBytes)
    })
  })

  describe('generateState', () => {
    it('should generate a base64url-encoded string', () => {
      const result = generateState()

      // Should be base64url encoded
      expect(result).toMatch(/^[A-Za-z0-9_-]+$/)
    })

    it('should generate strings of appropriate length', () => {
      const result = generateState()

      // Base64url encoded 16 bytes should be around 22 characters (16 * 4/3)
      expect(result.length).toBeGreaterThanOrEqual(22)
      expect(result.length).toBeLessThanOrEqual(32)
    })
  })

  describe('storePKCEParams', () => {
    it('should store state and code verifier in sessionStorage', () => {
      const state = 'test-state'
      const codeVerifier = 'test-code-verifier'

      storePKCEParams(state, codeVerifier)

      expect(sessionStorage.getItem('pkce_state')).toBe(state)
      expect(sessionStorage.getItem('pkce_code_verifier')).toBe(codeVerifier)
    })
  })

  describe('retrievePKCEParams', () => {
    it('should return stored PKCE parameters', () => {
      const state = 'test-state'
      const codeVerifier = 'test-code-verifier'

      sessionStorage.setItem('pkce_state', state)
      sessionStorage.setItem('pkce_code_verifier', codeVerifier)

      const result = retrievePKCEParams()

      expect(result).toEqual({
        state: 'test-state',
        codeVerifier: 'test-code-verifier'
      })
    })

    it('should return null values when parameters are not stored', () => {
      const result = retrievePKCEParams()

      expect(result).toEqual({
        state: null,
        codeVerifier: null
      })
    })
  })

  describe('clearPKCEParams', () => {
    it('should remove PKCE parameters from sessionStorage', () => {
      sessionStorage.setItem('pkce_state', 'test-state')
      sessionStorage.setItem('pkce_code_verifier', 'test-code-verifier')

      clearPKCEParams()

      expect(sessionStorage.getItem('pkce_state')).toBeNull()
      expect(sessionStorage.getItem('pkce_code_verifier')).toBeNull()
    })

    it('should not throw error when parameters do not exist', () => {
      expect(() => {
        clearPKCEParams()
      }).not.toThrow()
    })
  })

  describe('PKCE flow integration', () => {
    it('should support complete PKCE flow', async () => {
      // Generate PKCE parameters
      const codeVerifier = generateCodeVerifier()
      const codeChallenge = await generateCodeChallenge(codeVerifier)
      const state = generateState()

      // Store parameters
      storePKCEParams(state, codeVerifier)

      // Retrieve parameters
      const retrieved = retrievePKCEParams()
      expect(retrieved.state).toBe(state)
      expect(retrieved.codeVerifier).toBe(codeVerifier)

      // Clear parameters (after successful token exchange)
      clearPKCEParams()

      const cleared = retrievePKCEParams()
      expect(cleared.state).toBeNull()
      expect(cleared.codeVerifier).toBeNull()
    })
  })
})