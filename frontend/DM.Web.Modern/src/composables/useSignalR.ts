import { ref, onUnmounted, readonly } from 'vue'
import type { HubConnection } from '@microsoft/signalr'
import { HubConnectionState } from '@microsoft/signalr'
import api from '@/api'
import type { SignalRNotification, NotificationHandler } from '@/api/models/notifications/signalr'

/**
 * Composable for managing SignalR connection and notifications
 */
export function useSignalR() {
  const connection = ref<HubConnection | null>(null)
  const isConnected = ref(false)
  const connectionError = ref<string | null>(null)

  const handlers = new Set<NotificationHandler>()

  /**
   * Connect to the SignalR hub
   */
  async function connect(): Promise<boolean> {
    // Already connected
    if (connection.value?.state === HubConnectionState.Connected) {
      return true
    }

    // Not authenticated
    if (!api.isAuthenticated()) {
      connectionError.value = 'User must be authenticated to connect'
      return false
    }

    try {
      connectionError.value = null
      connection.value = api.establishHubConnection('whatsup')

      // Handle connection events
      connection.value.onclose((error) => {
        isConnected.value = false
        if (error) {
          connectionError.value = error.message
        }
      })

      connection.value.onreconnecting((error) => {
        isConnected.value = false
        if (error) {
          connectionError.value = `Reconnecting: ${error.message}`
        }
      })

      connection.value.onreconnected(() => {
        isConnected.value = true
        connectionError.value = null
      })

      // Register notification handler
      connection.value.on('Send', (notification: SignalRNotification) => {
        handlers.forEach(handler => handler(notification))
      })

      await connection.value.start()
      isConnected.value = true
      return true
    } catch (error) {
      connectionError.value = error instanceof Error ? error.message : 'Connection failed'
      isConnected.value = false
      return false
    }
  }

  /**
   * Disconnect from the SignalR hub
   */
  async function disconnect(): Promise<void> {
    if (connection.value) {
      try {
        await connection.value.stop()
      } catch {
        // Ignore errors during disconnect
      }
      connection.value = null
      isConnected.value = false
    }
  }

  /**
   * Subscribe to notifications
   * @param handler - Callback function to handle notifications
   * @returns Unsubscribe function
   */
  function onNotification(handler: NotificationHandler): () => void {
    handlers.add(handler)
    return () => handlers.delete(handler)
  }

  // Cleanup on component unmount
  onUnmounted(() => {
    disconnect()
    handlers.clear()
  })

  return {
    connect,
    disconnect,
    onNotification,
    isConnected: readonly(isConnected),
    connectionError: readonly(connectionError),
  }
}

/**
 * Singleton instance for app-wide SignalR connection
 */
let globalConnection: HubConnection | null = null
let globalHandlers = new Set<NotificationHandler>()
let globalIsConnected = ref(false)

/**
 * Global SignalR connection manager (singleton pattern)
 * Use this when you need a single connection shared across the app
 */
export function useGlobalSignalR() {
  async function connect(): Promise<boolean> {
    if (globalConnection?.state === HubConnectionState.Connected) {
      return true
    }

    if (!api.isAuthenticated()) {
      return false
    }

    try {
      globalConnection = api.establishHubConnection('whatsup')

      globalConnection.onclose(() => {
        globalIsConnected.value = false
      })

      globalConnection.onreconnected(() => {
        globalIsConnected.value = true
      })

      globalConnection.on('Send', (notification: SignalRNotification) => {
        globalHandlers.forEach(handler => handler(notification))
      })

      await globalConnection.start()
      globalIsConnected.value = true
      return true
    } catch {
      globalIsConnected.value = false
      return false
    }
  }

  async function disconnect(): Promise<void> {
    if (globalConnection) {
      try {
        await globalConnection.stop()
      } catch {
        // Ignore
      }
      globalConnection = null
      globalIsConnected.value = false
    }
  }

  function onNotification(handler: NotificationHandler): () => void {
    globalHandlers.add(handler)
    return () => globalHandlers.delete(handler)
  }

  return {
    connect,
    disconnect,
    onNotification,
    isConnected: readonly(globalIsConnected),
  }
}
