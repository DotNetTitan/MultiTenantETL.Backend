import * as signalR from '@microsoft/signalr'
import { API_CONFIG } from '@/config/api'

class ExecutionHubService {
  constructor() {
    this.connection = null
    this.isConnected = false
    this.reconnectAttempts = 0
    this.maxReconnectAttempts = 5
    this.handlers = {
      ExecutionStatusChanged: [],
      ExecutionProgressUpdated: [],
      ExecutionLogAdded: []
    }
  }

  /**
   * Initialize SignalR connection to the execution hub
   */
  async connect() {
    if (this.connection) {
      console.log('SignalR: Connection already exists')
      return
    }

    const token = localStorage.getItem('access_token')
    if (!token) {
      console.warn('SignalR: No access token available, cannot connect')
      return
    }

    // Build hub URL from API base URL
    const baseUrl = API_CONFIG.baseURL || window.location.origin
    const hubUrl = `${baseUrl}/hubs/executions`

    console.log('SignalR: Connecting to', hubUrl)

    this.connection = new signalR.HubConnectionBuilder()
      .withUrl(hubUrl, {
        accessTokenFactory: () => localStorage.getItem('access_token') || ''
      })
      .withAutomaticReconnect({
        nextRetryDelayInMilliseconds: (retryContext) => {
          // Exponential backoff: 0s, 2s, 10s, 30s, 60s
          if (retryContext.previousRetryCount === 0) return 0
          if (retryContext.previousRetryCount === 1) return 2000
          if (retryContext.previousRetryCount === 2) return 10000
          if (retryContext.previousRetryCount === 3) return 30000
          return 60000
        }
      })
      .configureLogging(signalR.LogLevel.Information)
      .build()

    // Setup event handlers
    this.setupEventHandlers()

    // Setup connection lifecycle handlers
    this.connection.onreconnecting((error) => {
      console.log('SignalR: Reconnecting...', error)
      this.isConnected = false
      this.reconnectAttempts++
    })

    this.connection.onreconnected((connectionId) => {
      console.log('SignalR: Reconnected with ID:', connectionId)
      this.isConnected = true
      this.reconnectAttempts = 0
    })

    this.connection.onclose((error) => {
      console.log('SignalR: Connection closed', error)
      this.isConnected = false
      
      // Attempt to reconnect if not at max attempts
      if (this.reconnectAttempts < this.maxReconnectAttempts) {
        setTimeout(() => {
          if (!this.isConnected) {
            console.log('SignalR: Attempting manual reconnect...')
            this.connect()
          }
        }, 5000)
      }
    })

    try {
      await this.connection.start()
      this.isConnected = true
      this.reconnectAttempts = 0
      console.log('SignalR: Connected successfully')
    } catch (error) {
      console.error('SignalR: Failed to connect', error)
      this.isConnected = false
      throw error
    }
  }

  /**
   * Setup SignalR event handlers
   */
  setupEventHandlers() {
    if (!this.connection) return

    // ExecutionStatusChanged event
    this.connection.on('ExecutionStatusChanged', (update) => {
      console.log('SignalR: ExecutionStatusChanged', update)
      this.handlers.ExecutionStatusChanged.forEach(handler => {
        try {
          handler(update)
        } catch (error) {
          console.error('Error in ExecutionStatusChanged handler:', error)
        }
      })
    })

    // ExecutionProgressUpdated event
    this.connection.on('ExecutionProgressUpdated', (update) => {
      console.log('SignalR: ExecutionProgressUpdated', update)
      this.handlers.ExecutionProgressUpdated.forEach(handler => {
        try {
          handler(update)
        } catch (error) {
          console.error('Error in ExecutionProgressUpdated handler:', error)
        }
      })
    })

    // ExecutionLogAdded event
    this.connection.on('ExecutionLogAdded', (data) => {
      console.log('SignalR: ExecutionLogAdded', data)
      this.handlers.ExecutionLogAdded.forEach(handler => {
        try {
          handler(data)
        } catch (error) {
          console.error('Error in ExecutionLogAdded handler:', error)
        }
      })
    })
  }

  /**
   * Register a handler for ExecutionStatusChanged events
   */
  onStatusChanged(handler) {
    this.handlers.ExecutionStatusChanged.push(handler)
    return () => {
      const index = this.handlers.ExecutionStatusChanged.indexOf(handler)
      if (index > -1) {
        this.handlers.ExecutionStatusChanged.splice(index, 1)
      }
    }
  }

  /**
   * Register a handler for ExecutionProgressUpdated events
   */
  onProgressUpdated(handler) {
    this.handlers.ExecutionProgressUpdated.push(handler)
    return () => {
      const index = this.handlers.ExecutionProgressUpdated.indexOf(handler)
      if (index > -1) {
        this.handlers.ExecutionProgressUpdated.splice(index, 1)
      }
    }
  }

  /**
   * Register a handler for ExecutionLogAdded events
   */
  onLogAdded(handler) {
    this.handlers.ExecutionLogAdded.push(handler)
    return () => {
      const index = this.handlers.ExecutionLogAdded.indexOf(handler)
      if (index > -1) {
        this.handlers.ExecutionLogAdded.splice(index, 1)
      }
    }
  }

  /**
   * Disconnect from the hub
   */
  async disconnect() {
    if (this.connection) {
      try {
        await this.connection.stop()
        console.log('SignalR: Disconnected')
      } catch (error) {
        console.error('SignalR: Error disconnecting', error)
      }
      this.connection = null
      this.isConnected = false
    }
  }

  /**
   * Get connection status
   */
  getConnectionStatus() {
    return {
      isConnected: this.isConnected,
      state: this.connection?.state || 'Disconnected'
    }
  }
}

// Export singleton instance
export const executionHubService = new ExecutionHubService()
