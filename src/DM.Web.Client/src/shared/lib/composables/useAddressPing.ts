/**
 * Live reachability of the site's addresses
 * @module shared/lib/composables/useAddressPing
 *
 * Measures every SITE_ADDRESSES host from the visitor's own network: the
 * block that lists the addresses shows which of them answer THIS reader and
 * how fast, which no server-side check can know. One tiny static file is
 * fetched off each origin and the round trip is timed around the await.
 *
 * /favicon.ico with a cache-busting query and `cache: "no-store"`: the one
 * file every host serves without auth, and a cached copy would measure the
 * browser rather than the network. `mode: "no-cors"` because the other
 * address is a foreign origin that sets no CORS headers — the response comes
 * back opaque, and an opaque response resolving at all is the whole fact
 * being measured: the address answers. (On the address being read the same
 * request is same-origin and resolves as a basic response — same fact.) A
 * fetch that rejects — network refusal or the timeout's abort — is the other
 * fact.
 *
 * Each host is asked twice in a row and the second round trip is the one
 * reported: the first carries the DNS lookup and the TLS handshake and reads
 * about twice the working figure, which is the number a visitor would then
 * compare the other address against. The first request still decides the
 * fact that matters most — whether the address answers at all.
 *
 * Measured on mount, again when the tab regains focus (at most once per
 * REMEASURE_MIN_INTERVAL_MS), and again when the network itself changes —
 * the device leaving wifi for mobile data, a VPN switched on or off — because
 * that is the one moment the answer actually changes and the tab would
 * otherwise not learn of it until its next return. Deliberately no interval
 * timer: a tab pinging two hosts forever is traffic nobody reads, and the
 * reading is only worth refreshing when somebody is about to look at it. Not
 * measured at all under `navigator.connection.saveData` — the extra requests
 * are exactly what that flag asks to avoid; the rows then keep their reserved
 * dash.
 */
import { onMounted, onUnmounted, reactive } from "vue";
import { SITE_ADDRESSES } from "@/shared/config/site";
import { savingData, onConnectionChange } from "@/shared/lib/utils/connection";

/** What the block knows about one address at any moment. */
export interface AddressPing {
  /** "pending" until the first measurement lands; then "up" or "down". */
  status: "pending" | "up" | "down";
  /** Round trip in whole milliseconds; null unless the status is "up". */
  latencyMs: number | null;
}

/**
 * Past this the round is aborted and the address counts as not answering.
 * Generous on purpose: a server that answers in four seconds is a bad server,
 * but it is not the dead one the block exists to expose.
 */
const PING_TIMEOUT_MS = 5_000;

/**
 * Tab focus re-measures, but not more often than this. A reader flipping
 * between tabs should not turn the sidebar into a ping flood; latency that is
 * three minutes old is still an honest picture of the network.
 */
const REMEASURE_MIN_INTERVAL_MS = 3 * 60 * 1000;

/**
 * A network change re-measures past the three-minute rule — it is the event
 * the rule exists to approximate — but the browser can report one change as
 * several events in a row, and this keeps them from becoming several rounds.
 */
const NETWORK_CHANGE_GUARD_MS = 5_000;

/** One round trip to the host, timed. Rejects when the host does not answer. */
async function roundTrip(host: string, signal: AbortSignal): Promise<number> {
  const started = performance.now();
  await fetch(`https://${host}/favicon.ico?ping=${Date.now()}`, {
    mode: "no-cors",
    cache: "no-store",
    signal,
  });
  return Math.round(performance.now() - started);
}

/**
 * Whether one address answers this reader right now.
 *
 * The same fact the block measures, asked once and outside any component: the
 * failure interceptor needs it the moment a request comes back with nothing,
 * and what it does with the answer is decide whether offering the other
 * address is help or a wild goose chase. A request that never reached the
 * server means one of two things - the server is down, or the road to THIS
 * address is closed - and only the second one the other address can fix.
 */
export async function addressAnswers(
  host: string,
  timeoutMs: number = PING_TIMEOUT_MS,
): Promise<boolean> {
  const controller = new AbortController();
  const timeout = setTimeout(() => controller.abort(), timeoutMs);
  try {
    await roundTrip(host, controller.signal);
    return true;
  } catch {
    return false;
  } finally {
    clearTimeout(timeout);
  }
}

/**
 * Reachability of every site address, keyed by host, live-updated as the
 * measurements land. Must be called during setup: the measurement starts on
 * mount and the focus listener is released in `onUnmounted`.
 */
export function useAddressPing(): Record<string, AddressPing> {
  const pings = reactive<Record<string, AddressPing>>(
    Object.fromEntries(
      SITE_ADDRESSES.map((address): [string, AddressPing] => [
        address.host,
        { status: "pending", latencyMs: null },
      ]),
    ),
  );

  let lastMeasuredAt = 0;
  let round: AbortController | null = null;

  async function probe(host: string, signal: AbortSignal): Promise<void> {
    try {
      // The first trip settles whether the address answers and pays for the
      // connection; the second is the figure a visitor can compare.
      await roundTrip(host, signal);
      const warm = await roundTrip(host, signal);
      pings[host] = { status: "up", latencyMs: warm };
    } catch {
      // Includes the unmount abort: writing "down" into state nobody reads
      // any more is harmless, and cheaper than telling the cases apart.
      pings[host] = { status: "down", latencyMs: null };
    }
  }

  function measure(): void {
    if (savingData()) return;
    lastMeasuredAt = Date.now();
    const controller = new AbortController();
    round = controller;
    // One timeout arms the whole round: both probes start together, and
    // aborting an already-resolved fetch is a no-op.
    const timeout = setTimeout(() => controller.abort(), PING_TIMEOUT_MS);
    void Promise.allSettled(
      SITE_ADDRESSES.map((address) => probe(address.host, controller.signal)),
    ).then(() => clearTimeout(timeout));
  }

  function onVisibilityChange(): void {
    if (document.visibilityState !== "visible") return;
    if (Date.now() - lastMeasuredAt < REMEASURE_MIN_INTERVAL_MS) return;
    measure();
  }

  function onNetworkChange(): void {
    if (Date.now() - lastMeasuredAt < NETWORK_CHANGE_GUARD_MS) return;
    // A round already in flight was measuring the network that just went
    // away; its answers would be about the wrong network.
    round?.abort();
    measure();
  }

  let stopNetworkListener: (() => void) | null = null;

  onMounted(() => {
    measure();
    document.addEventListener("visibilitychange", onVisibilityChange);
    stopNetworkListener = onConnectionChange(onNetworkChange);
  });

  onUnmounted(() => {
    document.removeEventListener("visibilitychange", onVisibilityChange);
    stopNetworkListener?.();
    round?.abort();
  });

  return pings;
}
