import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest'

// Mock Vue's inject before importing the composable
vi.mock('vue', () => ({
  inject: vi.fn()
}))

import { useGlobalState } from '@/composables/useGlobalState'
import { inject } from 'vue'

describe('useGlobalState', () => {
  const mockShowNotification = vi.fn()
  const mockRemoveNotification = vi.fn()
  const mockShowGlobalLoading = vi.fn()
  const mockHideGlobalLoading = vi.fn()
  const mockHandleError = vi.fn()

  beforeEach(() => {
    vi.clearAllMocks()

    // Setup the inject mock to return our mock functions
    vi.mocked(inject).mockImplementation((key) => {
      const mocks = {
        showNotification: mockShowNotification,
        removeNotification: mockRemoveNotification,
        showGlobalLoading: mockShowGlobalLoading,
        hideGlobalLoading: mockHideGlobalLoading,
        handleError: mockHandleError
      }
      return mocks[key]
    })
  })

  it('should return all injected functions', () => {
    const globalState = useGlobalState()

    expect(globalState.showNotification).toBe(mockShowNotification)
    expect(globalState.removeNotification).toBe(mockRemoveNotification)
    expect(globalState.showGlobalLoading).toBe(mockShowGlobalLoading)
    expect(globalState.hideGlobalLoading).toBe(mockHideGlobalLoading)
    expect(globalState.handleError).toBe(mockHandleError)
  })

  describe('convenience methods', () => {
    it('should call showNotification with correct parameters for showSuccess', () => {
      const globalState = useGlobalState()

      globalState.showSuccess('Operation completed', 'Custom Title', 3000)

      expect(mockShowNotification).toHaveBeenCalledWith('Operation completed', 'success', 3000, 'Custom Title')
    })

    it('should use default parameters for showSuccess', () => {
      const globalState = useGlobalState()

      globalState.showSuccess('Operation completed')

      expect(mockShowNotification).toHaveBeenCalledWith('Operation completed', 'success', 5000, 'Success')
    })

    it('should call showNotification with correct parameters for showError', () => {
      const globalState = useGlobalState()

      globalState.showError('Something went wrong', 'Custom Error', 10000)

      expect(mockShowNotification).toHaveBeenCalledWith('Something went wrong', 'error', 10000, 'Custom Error')
    })

    it('should use default parameters for showError', () => {
      const globalState = useGlobalState()

      globalState.showError('Something went wrong')

      expect(mockShowNotification).toHaveBeenCalledWith('Something went wrong', 'error', 7000, 'Error')
    })

    it('should call showNotification with correct parameters for showWarning', () => {
      const globalState = useGlobalState()

      globalState.showWarning('Be careful', 'Custom Warning', 8000)

      expect(mockShowNotification).toHaveBeenCalledWith('Be careful', 'warning', 8000, 'Custom Warning')
    })

    it('should use default parameters for showWarning', () => {
      const globalState = useGlobalState()

      globalState.showWarning('Be careful')

      expect(mockShowNotification).toHaveBeenCalledWith('Be careful', 'warning', 6000, 'Warning')
    })

    it('should call showNotification with correct parameters for showInfo', () => {
      const globalState = useGlobalState()

      globalState.showInfo('Here is some info', 'Custom Info', 4000)

      expect(mockShowNotification).toHaveBeenCalledWith('Here is some info', 'info', 4000, 'Custom Info')
    })

    it('should use default parameters for showInfo', () => {
      const globalState = useGlobalState()

      globalState.showInfo('Here is some info')

      expect(mockShowNotification).toHaveBeenCalledWith('Here is some info', 'info', 5000, 'Info')
    })
  })

  describe('missing dependencies', () => {
    beforeEach(() => {
      // Mock console.warn to avoid console output during tests
      vi.spyOn(console, 'warn').mockImplementation(() => {})

      // Setup inject to return undefined for showNotification
      vi.mocked(inject).mockImplementation((key) => {
        if (key === 'showNotification') return undefined
        return vi.fn()
      })
    })

    afterEach(() => {
      vi.restoreAllMocks()
    })

    it('should warn when showNotification is not provided', () => {
      const consoleSpy = vi.spyOn(console, 'warn')

      useGlobalState()

      expect(consoleSpy).toHaveBeenCalledWith('useGlobalState: showNotification not provided')
    })

    it('should still return convenience methods even when showNotification is not provided', () => {
      const globalState = useGlobalState()

      // These should not throw errors, even though showNotification is undefined
      expect(() => {
        globalState.showSuccess('test')
        globalState.showError('test')
        globalState.showWarning('test')
        globalState.showInfo('test')
      }).not.toThrow()
    })
  })
})