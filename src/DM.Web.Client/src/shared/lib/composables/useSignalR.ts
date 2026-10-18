import { ref, readonly } from "vue";
import type { HubConnection, RetryContext } from "@microsoft/signalr";
import api from "@/shared/api";
import { hubRetryPolicy } from "@/shared/api/client";
import { useAuthStore } from "@/shared/stores";
import { startStopwatch } from "@/shared/lib/utils/keyedCache";
import type {
  SignalRNotification,
  NotificationHandler,
} from "@/shared/api/models/notifications";

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

// Callers that arrived while an attempt on the current socket was already in
// flight: they wait for its outcome instead of killing it (see connect()).
let settlementWaiters: ((connected: boolean) => void)[] = [];

/**
 * Publishes the outcome of an attempt: the connected flag every consumer
 * watches, and the waiters that asked for this very attempt. A reconnect that
 * is still running is not an outcome, so onreconnecting only lowers the flag.
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

// Retry of the very first start(). The automatic reconnect of the client only
// revives a connection that once started, so a first attempt that failed (an
// API still warming up after a deploy is enough) used to leave the tab without
// realtime until the reader happened to reload the page. The schedule is the
// hubRetryPolicy the reconnect itself follows: one answer to how long a reader
// may look at a stale screen. The loop runs outside the transition queue,
// because a wait of up to the policy ceiling inside the queue would hold up
// every connect and disconnect behind it, and a logout is one of them.
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
 * Cuts short the delay the loop is waiting out. Whether the loop then goes on
 * or ends is deliberately not decided here: the lease set is the single source
 * of truth for that, and the loop reads it after the wait. A cancellation flag
 * set here instead outlived the cycle it was meant for, because a loop that
 * sits inside an attempt rather than inside a wait has nothing to wake, and a
 * lease taken again right after would then find a socket nobody was bringing
 * up (no retries running, no connection).
 */
function wakeStartRetries(): void {
  cancelRetryWait?.();
}

function scheduleStartRetries(): void {
  if (startRetries) return;
  // How long the loop has been at it, which is half of what the policy is
  // handed. Measured by the shared stopwatch rather than by a clock reading
  // kept here, so the client subtracts a recorded moment from the clock in one
  // module and not in every module that happens to need elapsed time.
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
      // Read after the wait and never before it: disconnect() drops the lease
      // synchronously, and a tab whose last holder has left has nothing to
      // reconnect for. Read again rather than remembered, so a lease taken back
      // during this cycle (a sign-out with a sign-in right behind it, a page
      // swap) keeps the loop instead of ending it.
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
    // Anonymous connect is supported: auth travels in the HttpOnly session
    // cookie (withCredentials), guests simply negotiate without it and the
    // hub accepts them as receive-only broadcast listeners.
    negotiatedAsAuthenticated = useAuthStore().isAuthenticated;
    globalConnection = await api.establishHubConnection("hubs/notifications");

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
    // Ignore
  }
}

/**
 * What a connect() call learned inside the transition queue: either the
 * outcome, or the attempt that was already running and is to be awaited
 * outside the queue. Wrapped in an object so awaiting the queued operation
 * does not flatten the pending attempt into it.
 */
type ConnectOutcome = { connected: boolean } | { pending: Promise<boolean> };

/**
 * Global SignalR connection manager (singleton pattern)
 * Use this when you need a single connection shared across the app.
 *
 * @param owner - lease name for this consumer; connect() acquires the lease,
 * disconnect() releases it. The underlying socket is shared across owners.
 */
export function useGlobalSignalR(owner = "app") {
  async function connect(): Promise<boolean> {
    const outcome = await enqueueGlobalTransition<ConnectOutcome>(async () => {
      globalLeases.add(owner);

      // Imported here rather than at the top of the file: a static import of
      // the SignalR client puts it back on the critical path of every page
      // load. This block is already async, and by the time a connection exists
      // to compare against, the module is in the loader's cache.
      const { HubConnectionState } = await import("@microsoft/signalr");

      if (globalConnection?.state === HubConnectionState.Connected) {
        if (negotiatedAsAuthenticated === useAuthStore().isAuthenticated) {
          return { connected: true };
        }
        // Auth context changed since negotiation, so bounce to re-negotiate
        // with the current cookie state.
        await stopGlobalSocket();
      } else if (
        globalConnection?.state === HubConnectionState.Connecting ||
        globalConnection?.state === HubConnectionState.Reconnecting
      ) {
        // An attempt on this very socket is already running. Stopping it would
        // abort a connection about to come up and, on the reconnect path,
        // throw away the schedule the client is already following, leaving the
        // tab with no realtime at all. So this caller waits for that attempt.
        // The wait is handed back to be awaited outside the queue: reconnects
        // are unbounded and a disconnect must not sit behind one.
        return { pending: whenSettled() };
      } else if (globalConnection) {
        // Stale non-connected instance (failed or already closed), so restart
        // clean rather than racing its internal retry loop.
        await stopGlobalSocket();
      }

      const connected = await startGlobalSocket();
      if (!connected) scheduleStartRetries();
      return { connected };
    });

    // The attempt this caller waited for was somebody else's, and it says
    // nothing about the auth context: a sign-in through the modal while that
    // reconnect was running leaves the reader on the anonymous socket the
    // reconnect brought back. So a caller that waited asks again from the top.
    // The state is Connected by then, the comparison above sees the mismatch
    // and bounces, and the flag is fresh afterwards, which is what ends the
    // recursion.
    return "pending" in outcome
      ? outcome.pending.then((up) => (up ? connect() : false))
      : outcome.connected;
  }

  async function disconnect(): Promise<void> {
    // The lease leaves before the queue rather than inside it: the retry loop
    // reads the set between attempts, so a logout that only took effect once
    // the queue drained would keep reconnecting for the whole delay ceiling.
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

      // Other lease holders still need the socket. If this release came
      // from an auth change (App.vue disconnects on logout while the global
      // chat page holds its lease), re-negotiate anonymously instead of
      // keeping a connection the server still associates with the previous
      // user; otherwise leave the shared socket untouched.
      if (negotiatedAsAuthenticated !== useAuthStore().isAuthenticated) {
        await stopGlobalSocket();
        // A re-negotiation that refused leaves exactly the hole a refused
        // first start leaves: nothing revives a socket that never came up, and
        // the lease holders left behind (the global chat page, typically) would
        // sit without realtime until something else called connect(). Same
        // schedule, same loop.
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
