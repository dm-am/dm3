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
 * Measured on mount and again when the tab regains focus, at most once per
 * REMEASURE_MIN_INTERVAL_MS. Deliberately no interval timer: a tab pinging
 * two hosts forever is traffic nobody reads, and the reading is only worth
 * refreshing when somebody is about to look at it. Not measured at all under
 * `navigator.connection.saveData` — two extra requests are exactly what that
 * flag asks to avoid; the rows then keep their reserved dash.
 */
import { onMounted, onUnmounted, reactive } from "vue";
import { SITE_ADDRESSES } from "@/shared/config/site";
import { savingData } from "@/shared/lib/utils/connection";

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
    const started = performance.now();
    try {
      await fetch(`https://${host}/favicon.ico?ping=${Date.now()}`, {
        mode: "no-cors",
        cache: "no-store",
        signal,
      });
      pings[host] = {
        status: "up",
        latencyMs: Math.round(performance.now() - started),
      };
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

  onMounted(() => {
    measure();
    document.addEventListener("visibilitychange", onVisibilityChange);
  });

  onUnmounted(() => {
    document.removeEventListener("visibilitychange", onVisibilityChange);
    round?.abort();
  });

  return pings;
}
