import { inject } from 'vue'

/**
 * Composable for accessing global application state and utilities
 * Provides access to notifications, loading states, and error handling
 */
export function useGlobalState() {
  const showNotification = inject('showNotification')
  const removeNotification = inject('removeNotification')
  const showGlobalLoading = inject('showGlobalLoading')
  const hideGlobalLoading = inject('hideGlobalLoading')
  const handleError = inject('handleError')

  if (!showNotification) {
    console.warn('useGlobalState: showNotification not provided')
  }

  return {
    // Notification methods
    showNotification,
    removeNotification,
    
    // Loading methods
    showGlobalLoading,
    hideGlobalLoading,
    
    // Error handling
    handleError,
    
    // Convenience methods with optional title
    showSuccess: (message, title = 'Success', timeout = 5000) => {
      showNotification?.(message, 'success', timeout, title)
    },
    showError: (message, title = 'Error', timeout = 7000) => {
      showNotification?.(message, 'error', timeout, title)
    },
    showWarning: (message, title = 'Warning', timeout = 6000) => {
      showNotification?.(message, 'warning', timeout, title)
    },
    showInfo: (message, title = 'Info', timeout = 5000) => {
      showNotification?.(message, 'info', timeout, title)
    }
  }
}
