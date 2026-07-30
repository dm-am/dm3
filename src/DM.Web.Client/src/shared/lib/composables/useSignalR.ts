import { ref, readonly } from "vue";
import type { HubConnection } from "@microsoft/signalr";
import api from "@/shared/api";
import { useAuthStore } from "@/shared/stores";
import type {
  SignalRNotification,
  NotificationHandler,
} from "@/shared/api/models/notifications/signalr";

/**
 * Singleton instance for app-wide SignalR connection
 */
let globalConnection: HubConnection | null = null;
const globalHandlers = new Set<NotificationHandler>();
const globalIsConnected = ref(false);

// Lease-based ownership of the shared socket: App.vue holds the default
// "app" lease while the user is authenticated; pages that need realtime for
// guests too (global chat) hold their own named lease while mounted. The
// socket is stopped only when the last lease is released, so a page unmount
// never kills the app-owned connection and vice versa.
const globalLeases = new Set<string>();

// Auth state (session cookie presence) at the moment the current socket was
// negotiated. When it stops matching (login/logout happened while the socket
// stayed alive because another lease held it), the connection is bounced so
// the server re-reads the cookie — otherwise a logged-out browser would keep
// receiving the previous user's notifications, and a fresh login would stay
// on an anonymous connection and miss its per-user events.
let negotiatedAsAuthenticated: boolean | null = null;

// Serializes connect/disconnect transitions — App.vue and pages call into
// the singleton concurrently (e.g. both mount at once on a full page load).
let globalTransition: Promise<unknown> = Promise.resolve();

function enqueueGlobalTransition<T>(operation: () => Promise<T>): Promise<T> {
  const next = globalTransition.then(operation, operation);
  globalTransition = next.catch(() => undefined);
  return next;
}

async function startGlobalSocket(): Promise<boolean> {
  try {
    // Anonymous connect is supported: auth travels in the HttpOnly session
    // cookie (withCredentials), guests simply negotiate without it and the
    // hub accepts them as receive-only broadcast listeners.
    negotiatedAsAuthenticated = useAuthStore().isAuthenticated;
    globalConnection = await api.establishHubConnection("whatsup");

    globalConnection.onclose(() => {
      globalIsConnected.value = false;
    });

    globalConnection.onreconnecting(() => {
      globalIsConnected.value = false;
    });

    globalConnection.onreconnected(() => {
      globalIsConnected.value = true;
    });

    globalConnection.on("Send", (notification: SignalRNotification) => {
      globalHandlers.forEach((handler) => handler(notification));
    });

    await globalConnection.start();
    globalIsConnected.value = true;
    return true;
  } catch {
    globalConnection = null;
    globalIsConnected.value = false;
    return false;
  }
}

async function stopGlobalSocket(): Promise<void> {
  if (!globalConnection) return;
  const connection = globalConnection;
  globalConnection = null;
  globalIsConnected.value = false;
  try {
    await connection.stop();
  } catch {
    // Ignore
  }
}

/**
 * Global SignalR connection manager (singleton pattern)
 * Use this when you need a single connection shared across the app.
 *
 * @param owner - lease name for this consumer; connect() acquires the lease,
 * disconnect() releases it. The underlying socket is shared across owners.
 */
export function useGlobalSignalR(owner = "app") {
  async function connect(): Promise<boolean> {
    return enqueueGlobalTransition(async () => {
      globalLeases.add(owner);

      // Imported here rather than at the top of the file: a static import of
      // the SignalR client puts it back on the critical path of every page
      // load. This block is already async, and by the time a connection exists
      // to compare against, the module is in the loader's cache.
      const { HubConnectionState } = await import("@microsoft/signalr");

      if (globalConnection?.state === HubConnectionState.Connected) {
        if (negotiatedAsAuthenticated === useAuthStore().isAuthenticated) {
          return true;
        }
        // Auth context changed since negotiation — bounce to re-negotiate
        // with the current cookie state.
        await stopGlobalSocket();
      } else if (globalConnection) {
        // Stale non-connected instance (failed or mid-reconnect) — restart
        // clean rather than racing its internal retry loop.
        await stopGlobalSocket();
      }

      return startGlobalSocket();
    });
  }

  async function disconnect(): Promise<void> {
    return enqueueGlobalTransition(async () => {
      globalLeases.delete(owner);
      if (!globalConnection) return;

      if (globalLeases.size === 0) {
        await stopGlobalSocket();
        return;
      }

      // Other lease holders still need the socket. If this release came
      // from an auth change (App.vue disconnects on logout while the global
      // chat page holds its lease), re-negotiate anonymously instead of
      // keeping a connection the server still associates with the previous
      // user; otherwise leave the shared socket untouched.
      if (negotiatedAsAuthenticated !== useAuthStore().isAuthenticated) {
        await stopGlobalSocket();
        await startGlobalSocket();
      }
    });
  }

  function onNotification(handler: NotificationHandler): () => void {
    globalHandlers.add(handler);
    return () => globalHandlers.delete(handler);
  }

  return {
    connect,
    disconnect,
    onNotification,
    isConnected: readonly(globalIsConnected),
  };
}
