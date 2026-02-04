import { ref, onMounted, onBeforeUnmount } from 'vue'
import { executionHubService } from '@/services/executionHubService'

export function useExecutionHub() {
  const isConnected = ref(false)
  const connectionError = ref(null)

  // Event handlers cleanup functions
  let cleanupFunctions = []

  /**
   * Connect to the execution hub
   */
  const connect = async () => {
    try {
      connectionError.value = null
      await executionHubService.connect()
      isConnected.value = true
    } catch (error) {
      console.error('Failed to connect to execution hub:', error)
      connectionError.value = error.message || 'Connection failed'
      isConnected.value = false
    }
  }

  /**
   * Disconnect from the execution hub
   */
  const disconnect = async () => {
    try {
      await executionHubService.disconnect()
      isConnected.value = false
    } catch (error) {
      console.error('Failed to disconnect from execution hub:', error)
    }
  }

  /**
   * Register handler for execution status changes
   */
  const onStatusChanged = (handler) => {
    const cleanup = executionHubService.onStatusChanged(handler)
    cleanupFunctions.push(cleanup)
    return cleanup
  }

  /**
   * Register handler for execution progress updates
   */
  const onProgressUpdated = (handler) => {
    const cleanup = executionHubService.onProgressUpdated(handler)
    cleanupFunctions.push(cleanup)
    return cleanup
  }

  /**
   * Register handler for execution log additions
   */
  const onLogAdded = (handler) => {
    const cleanup = executionHubService.onLogAdded(handler)
    cleanupFunctions.push(cleanup)
    return cleanup
  }

  /**
   * Get current connection status
   */
  const getStatus = () => {
    const status = executionHubService.getConnectionStatus()
    isConnected.value = status.isConnected
    return status
  }

  // Auto-connect on mount
  onMounted(() => {
    connect()
  })

  // Auto-disconnect and cleanup on unmount
  onBeforeUnmount(() => {
    cleanupFunctions.forEach(cleanup => cleanup())
    cleanupFunctions = []
    disconnect()
  })

  return {
    isConnected,
    connectionError,
    connect,
    disconnect,
    onStatusChanged,
    onProgressUpdated,
    onLogAdded,
    getStatus
  }
}
