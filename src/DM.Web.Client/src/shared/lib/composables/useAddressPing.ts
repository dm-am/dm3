/**
 * Whether each SITE_ADDRESSES host answers this reader and how fast, timed
 * around a no-store, no-cors fetch of /favicon.ico: an opaque answer at all is
 * the fact. The second round trip is reported, the first carries DNS and TLS.
 * Measured on mount, on focus (at most once per REMEASURE_MIN_INTERVAL_MS) and
 * on a network change, never on a timer and never under saveData.
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

/** Past this the address counts as not answering: slow is not dead. */
const PING_TIMEOUT_MS = 5_000;

/** Tab focus re-measures no more often than this. */
const REMEASURE_MIN_INTERVAL_MS = 3 * 60 * 1000;

/** A network change re-measures past the interval; the browser may report it several times. */
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
 * Whether one address answers this reader now, for the failure interceptor:
 * offering the other address helps only when the road to this one is closed.
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

/** Reachability by host, live; call during setup (measures on mount). */
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
