/**
 * @vitest-environment node
 */

/**
 * The socket both header badges live on has exactly one way back from a first
 * start() that failed, and it is the loop in this composable: the automatic
 * reconnect of the SignalR client only revives a connection that once started,
 * so an attempt that never succeeded is retried here or nowhere. An API still
 * warming up after a deploy is enough to lose that first attempt, and what
 * used to follow was a tab with no realtime until the reader reloaded it.
 *
 * Two things about the loop are invisible in a rendered page and easy to break:
 * the schedule it follows (retrying at once hammers the very API that is not
 * ready yet) and the way out of it (a logout that has to sit through the delay
 * ceiling before the tab stops reconnecting). The third case here is the one
 * that used to throw away a working recovery: connect() arriving while the
 * client's own reconnect was in flight stopped the instance, and with it the
 * schedule already running.
 *
 * The rest is about who the socket was negotiated as. A sign-in or a sign-out
 * that lands while somebody else holds the socket up has to reach the server,
 * because the session cookie is read at negotiation and never again: a socket
 * left as it was either misses everything addressed to the new session, or
 * keeps delivering the previous one's events to a browser that has signed out.
 */
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { HubConnectionState } from "@microsoft/signalr";
import { hubRetryPolicy } from "@/shared/api/client";

const { establishHubConnection } = vi.hoisted(() => ({
  establishHubConnection: vi.fn(),
}));

vi.mock("@/shared/api", () => ({
  default: { establishHubConnection },
}));

// The socket negotiates as whoever the session store says is signed in, and
// two tests here flip that while an attempt is in flight, so the stub reads a
// value the test can move and pinia stays out of the file.
const authState = vi.hoisted(() => ({ isAuthenticated: false }));

vi.mock("@/shared/stores", () => ({
  useAuthStore: () => ({ isAuthenticated: authState.isAuthenticated }),
}));

/** What the retry schedule allows before attempt number `previousRetryCount`. */
const delayBefore = (previousRetryCount: number) =>
  hubRetryPolicy.nextRetryDelayInMilliseconds({
    previousRetryCount,
    elapsedMilliseconds: 0,
    retryReason: new Error("Initial hub connection attempt failed"),
  }) as number;

/** A hub connection whose start() either comes up or refuses. */
function fakeConnection(succeeds: boolean) {
  const callbacks: {
    closed?: () => void;
    reconnecting?: () => void;
    reconnected?: () => void;
  } = {};
  const connection = {
    state: HubConnectionState.Disconnected as HubConnectionState,
    callbacks,
    start: vi.fn(async () => {
      if (!succeeds) throw new Error("negotiation refused");
      connection.state = HubConnectionState.Connected;
    }),
    stop: vi.fn(async () => {
      connection.state = HubConnectionState.Disconnected;
    }),
    on: vi.fn(),
    onclose: vi.fn((callback: () => void) => {
      callbacks.closed = callback;
    }),
    onreconnecting: vi.fn((callback: () => void) => {
      callbacks.reconnecting = callback;
    }),
    onreconnected: vi.fn((callback: () => void) => {
      callbacks.reconnected = callback;
    }),
  };
  return connection;
}

/** The composable keeps module-level state, so every test gets it fresh. */
async function loadSocket(owner = "app") {
  const { useGlobalSignalR } = await import("./useSignalR");
  return useGlobalSignalR(owner);
}

/** Lets the transition queue run to its next real pause. */
const settleQueue = () => new Promise((resolve) => setTimeout(resolve, 0));

describe("the shared notification socket", () => {
  beforeEach(() => {
    vi.resetModules();
    establishHubConnection.mockReset();
    authState.isAuthenticated = false;
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it("retries a failed first start on the reconnect schedule", async () => {
    vi.useFakeTimers();
    let attempts = 0;
    establishHubConnection.mockImplementation(async () => {
      attempts += 1;
      return fakeConnection(attempts >= 3);
    });

    const socket = await loadSocket();
    await expect(socket.connect()).resolves.toBe(false);
    expect(establishHubConnection).toHaveBeenCalledTimes(1);

    // Not a millisecond before the policy allows it.
    await vi.advanceTimersByTimeAsync(delayBefore(0) - 1);
    expect(establishHubConnection).toHaveBeenCalledTimes(1);

    await vi.advanceTimersByTimeAsync(1);
    expect(establishHubConnection).toHaveBeenCalledTimes(2);
    expect(socket.isConnected.value).toBe(false);

    await vi.advanceTimersByTimeAsync(delayBefore(1));
    expect(establishHubConnection).toHaveBeenCalledTimes(3);
    expect(socket.isConnected.value).toBe(true);

    // And the loop ends with the socket up rather than reconnecting over it.
    await vi.advanceTimersByTimeAsync(60_000);
    expect(establishHubConnection).toHaveBeenCalledTimes(3);
  });

  it("stops retrying when the last lease is released", async () => {
    vi.useFakeTimers();
    establishHubConnection.mockImplementation(async () =>
      fakeConnection(false),
    );

    const socket = await loadSocket();
    await expect(socket.connect()).resolves.toBe(false);
    expect(establishHubConnection).toHaveBeenCalledTimes(1);

    // A sign-out during the wait: it takes effect now, not at the end of the
    // delay the loop happens to be sitting in.
    await socket.disconnect();

    await vi.advanceTimersByTimeAsync(60_000);
    expect(establishHubConnection).toHaveBeenCalledTimes(1);
  });

  it("keeps retrying when the lease is taken back during an attempt", async () => {
    vi.useFakeTimers();
    let attempts = 0;
    let refuseHanging: () => void = () => undefined;
    const hanging = new Promise<never>((_, reject) => {
      refuseHanging = () => reject(new Error("negotiation refused"));
    });
    establishHubConnection.mockImplementation(async () => {
      attempts += 1;
      // The second attempt hangs on negotiation. That is what puts the loop
      // inside an attempt rather than inside a wait, and a loop that is not
      // waiting has nothing anybody can wake.
      if (attempts === 2) return hanging;
      return fakeConnection(attempts >= 4);
    });

    const socket = await loadSocket();
    await expect(socket.connect()).resolves.toBe(false);

    await vi.advanceTimersByTimeAsync(delayBefore(0));
    expect(establishHubConnection).toHaveBeenCalledTimes(2);

    // A sign-out with a sign-in right behind it, both landing while that
    // attempt is still in flight: they queue behind it, and the lease the
    // second one takes is the one the loop reads when it wakes up.
    const released = socket.disconnect();
    const retaken = socket.connect();
    refuseHanging();
    await released;
    await expect(retaken).resolves.toBe(false);
    expect(establishHubConnection).toHaveBeenCalledTimes(3);

    // The lease is live and the socket is down, so the loop is once again the
    // only thing that can bring it up.
    await vi.advanceTimersByTimeAsync(60_000);
    expect(establishHubConnection).toHaveBeenCalledTimes(4);
    expect(socket.isConnected.value).toBe(true);
  });

  it("waits for a reconnect in flight instead of stopping it", async () => {
    const connection = fakeConnection(true);
    establishHubConnection.mockResolvedValue(connection);

    const socket = await loadSocket();
    await expect(socket.connect()).resolves.toBe(true);

    // The connection dropped and the client is running its own schedule.
    connection.state = HubConnectionState.Reconnecting;
    connection.callbacks.reconnecting?.();
    expect(socket.isConnected.value).toBe(false);

    const pending = socket.connect();
    await settleQueue();

    expect(connection.stop).not.toHaveBeenCalled();
    expect(establishHubConnection).toHaveBeenCalledTimes(1);
    expect(socket.isConnected.value).toBe(false);

    connection.state = HubConnectionState.Connected;
    connection.callbacks.reconnected?.();
    await expect(pending).resolves.toBe(true);
    expect(socket.isConnected.value).toBe(true);
  });

  it("re-reads the auth context after a reconnect it only waited for", async () => {
    const anonymous = fakeConnection(true);
    const authenticated = fakeConnection(true);
    establishHubConnection
      .mockResolvedValueOnce(anonymous)
      .mockResolvedValueOnce(authenticated);

    const socket = await loadSocket();
    await expect(socket.connect()).resolves.toBe(true);

    anonymous.state = HubConnectionState.Reconnecting;
    anonymous.callbacks.reconnecting?.();

    const pending = socket.connect();
    await settleQueue();

    // The reader signs in through the modal while that reconnect is running.
    // What comes back up is the socket negotiated before the sign-in, so every
    // event addressed to the new session would go past this tab.
    authState.isAuthenticated = true;
    anonymous.state = HubConnectionState.Connected;
    anonymous.callbacks.reconnected?.();

    await expect(pending).resolves.toBe(true);
    expect(anonymous.stop).toHaveBeenCalledTimes(1);
    expect(establishHubConnection).toHaveBeenCalledTimes(2);
    expect(socket.isConnected.value).toBe(true);
  });

  it("retries a re-negotiation that refused under another lease", async () => {
    vi.useFakeTimers();
    let attempts = 0;
    establishHubConnection.mockImplementation(async () => {
      attempts += 1;
      // The signed-in socket, then the anonymous re-negotiation that refuses,
      // then the attempt the schedule brings.
      return fakeConnection(attempts !== 2);
    });

    authState.isAuthenticated = true;
    const app = await loadSocket();
    const page = await loadSocket("global-chat");
    await expect(app.connect()).resolves.toBe(true);
    await expect(page.connect()).resolves.toBe(true);
    expect(establishHubConnection).toHaveBeenCalledTimes(1);

    // The reader signs out. The chat page still holds its lease, so the socket
    // is re-negotiated anonymously rather than stopped, and this time the
    // negotiation refuses.
    authState.isAuthenticated = false;
    await app.disconnect();
    expect(establishHubConnection).toHaveBeenCalledTimes(2);
    expect(page.isConnected.value).toBe(false);

    await vi.advanceTimersByTimeAsync(delayBefore(0));
    expect(establishHubConnection).toHaveBeenCalledTimes(3);
    expect(page.isConnected.value).toBe(true);
  });
});
