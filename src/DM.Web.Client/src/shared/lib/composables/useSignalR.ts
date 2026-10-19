import { ref, readonly } from "vue";
import type { HubConnection, RetryContext } from "@microsoft/signalr";
import { Api } from "@/shared/api";
import { hubRetryPolicy } from "@/shared/api/client";
import { useAuthStore } from "@/shared/stores";
import { startStopwatch } from "@/shared/lib/utils/keyedCache";
import type {
  SignalRNotification,
  NotificationHandler,
} from "@/shared/api/models/notifications";

let globalConnection: HubConnection | null = null;
const globalHandlers = new Set<NotificationHandler>();
const globalIsConnected = ref(false);

// The socket is shared by leases: App.vue holds "app" while signed in, a page
// with realtime for guests (global chat) holds its own while mounted; the
// socket stops when the last lease goes.
const globalLeases = new Set<string>();

// Whether the socket was negotiated signed in. A login or logout under another
// lease bounces it, or the browser would keep the previous user's events.
let negotiatedAsAuthenticated: boolean | null = null;

// Serializes connect and disconnect: App.vue and pages call in at once.
let globalTransition: Promise<unknown> = Promise.resolve();

function enqueueGlobalTransition<T>(operation: () => Promise<T>): Promise<T> {
  const next = globalTransition.then(operation, operation);
  globalTransition = next.catch(() => undefined);
  return next;
}

// Callers that wait for the attempt in flight instead of killing it.
let settlementWaiters: ((connected: boolean) => void)[] = [];

/**
 * Publishes the outcome of an attempt to the flag and to its waiters; a
 * running reconnect is no outcome, so onreconnecting only lowers the flag.
 */
function settleConnection(connected: boolean): void {
  globalIsConnected.value = connected;
  const waiters = settlementWaiters;
  settlementWaiters = [];
  waiters.forEach((resolve) => resolve(connected));
}

function whenSettled(): Promise<boolean> {
  return new Promise((resolve) => settlementWaiters.push(resolve));
}

// Retries of the first start(), which the client's automatic reconnect does
// not cover, on the hubRetryPolicy schedule. Outside the transition queue, or
// a wait of up to the ceiling would hold a logout behind it.
let startRetries: Promise<void> | null = null;
let cancelRetryWait: (() => void) | null = null;

function waitBeforeRetry(delayMs: number): Promise<void> {
  return new Promise((resolve) => {
    const timer = setTimeout(() => {
      cancelRetryWait = null;
      resolve();
    }, delayMs);
    cancelRetryWait = () => {
      clearTimeout(timer);
      cancelRetryWait = null;
      resolve();
    };
  });
}

/**
 * Cuts the wait short; whether the loop goes on is read from the leases after
 * the wait, since a flag set here would outlive its cycle.
 */
function wakeStartRetries(): void {
  cancelRetryWait?.();
}

function scheduleStartRetries(): void {
  if (startRetries) return;
  // Elapsed time by the shared stopwatch, not a clock reading kept here.
  const retryingSince = startStopwatch();
  startRetries = (async () => {
    for (let previousRetryCount = 0; ; previousRetryCount++) {
      const context: RetryContext = {
        previousRetryCount,
        elapsedMilliseconds: retryingSince.elapsedMs(),
        retryReason: new Error("Initial hub connection attempt failed"),
      };
      const delay = hubRetryPolicy.nextRetryDelayInMilliseconds(context);
      // The policy retries forever; null is how a policy says it gave up.
      if (delay === null || delay === undefined) break;

      await waitBeforeRetry(delay);
      // Read after the wait and afresh: disconnect() drops the lease at once,
      // and a lease taken back during the cycle keeps the loop.
      if (globalLeases.size === 0) break;

      const done = await enqueueGlobalTransition(async () => {
        if (globalLeases.size === 0) return true;
        // Somebody connected while this attempt was queued.
        if (globalConnection) return true;
        return startGlobalSocket();
      });
      if (done) break;
    }
    startRetries = null;
  })();
}

async function startGlobalSocket(): Promise<boolean> {
  try {
    // Guests connect too, without the session cookie, as broadcast listeners.
    negotiatedAsAuthenticated = useAuthStore().isAuthenticated;
    globalConnection = await Api.establishHubConnection("hubs/notifications");

    globalConnection.onclose(() => {
      settleConnection(false);
    });

    globalConnection.onreconnecting(() => {
      globalIsConnected.value = false;
    });

    globalConnection.onreconnected(() => {
      settleConnection(true);
    });

    globalConnection.on("Send", (notification: SignalRNotification) => {
      globalHandlers.forEach((handler) => handler(notification));
    });

    await globalConnection.start();
    settleConnection(true);
    return true;
  } catch {
    globalConnection = null;
    settleConnection(false);
    return false;
  }
}

async function stopGlobalSocket(): Promise<void> {
  if (!globalConnection) return;
  const connection = globalConnection;
  globalConnection = null;
  settleConnection(false);
  try {
    await connection.stop();
  } catch {
    // A dying connection may refuse to stop; it is dropped either way.
  }
}

/**
 * The outcome of connect() in the queue, or the attempt running already, in an
 * object so awaiting the queue does not flatten the pending promise.
 */
type ConnectOutcome = { connected: boolean } | { pending: Promise<boolean> };

/**
 * The app-wide SignalR connection.
 *
 * @param owner - lease name for this consumer; connect() acquires the lease,
 * disconnect() releases it. The underlying socket is shared across owners.
 */
export function useGlobalSignalR(owner = "app") {
  async function connect(): Promise<boolean> {
    const outcome = await enqueueGlobalTransition<ConnectOutcome>(async () => {
      globalLeases.add(owner);

      // Imported here to keep the SignalR client off the critical path.
      const { HubConnectionState } = await import("@microsoft/signalr");

      if (globalConnection?.state === HubConnectionState.Connected) {
        if (negotiatedAsAuthenticated === useAuthStore().isAuthenticated) {
          return { connected: true };
        }
        // The auth changed since negotiation: re-negotiate with the cookie now.
        await stopGlobalSocket();
      } else if (
        globalConnection?.state === HubConnectionState.Connecting ||
        globalConnection?.state === HubConnectionState.Reconnecting
      ) {
        // An attempt on this socket is running: wait for it outside the queue
        // rather than abort it, since reconnects are unbounded.
        return { pending: whenSettled() };
      } else if (globalConnection) {
        // A failed or closed instance: restart clean.
        await stopGlobalSocket();
      }

      const connected = await startGlobalSocket();
      if (!connected) scheduleStartRetries();
      return { connected };
    });

    // The attempt waited for was somebody else's and may be anonymous: ask
    // again, which bounces on a mismatch and then ends.
    return "pending" in outcome
      ? outcome.pending.then((up) => (up ? connect() : false))
      : outcome.connected;
  }

  async function disconnect(): Promise<void> {
    // Before the queue: the retry loop reads the leases between attempts.
    globalLeases.delete(owner);
    if (globalLeases.size === 0) wakeStartRetries();

    return enqueueGlobalTransition(async () => {
      // Again, because a connect() queued ahead of this call re-adds the lease.
      globalLeases.delete(owner);
      if (!globalConnection) return;

      if (globalLeases.size === 0) {
        await stopGlobalSocket();
        return;
      }

      // Others still hold the socket; an auth change re-negotiates it
      // anonymously.
      if (negotiatedAsAuthenticated !== useAuthStore().isAuthenticated) {
        await stopGlobalSocket();
        // A refused re-negotiation is retried like a refused first start.
        if (!(await startGlobalSocket())) scheduleStartRetries();
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
