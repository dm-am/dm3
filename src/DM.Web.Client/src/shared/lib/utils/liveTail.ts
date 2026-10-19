/**
 * How close to the end of a list still counts as the end, for every list that
 * appends to its bottom: browsers report fractional scroll positions.
 */
export const AT_BOTTOM_THRESHOLD_PX = 40;

/** How often a live list re-reads its tail while the socket delivers nothing. */
export const LIVE_TAIL_POLL_INTERVAL_MS = 30000;

/** Measured each time: appending grows scrollHeight without a scroll event. */
export function isNearBottom(
  el: HTMLElement,
  thresholdPx: number = AT_BOTTOM_THRESHOLD_PX,
): boolean {
  return el.scrollHeight - el.scrollTop - el.clientHeight < thresholdPx;
}

/**
 * What the newest page does to the window: `none` (empty), `gap` (shares
 * nothing with a window that holds some, so the window is replaced), `append`
 * (the items it does not hold yet, in order).
 */
type TailUpdate<T> =
  | { kind: "none" }
  | { kind: "append"; fresh: T[] }
  | { kind: "gap" };

/** Decides the newest page against the window, as API_DESIGN.md ("Realtime") states. */
export function tailUpdate<T extends { id: unknown }>(
  shown: readonly T[],
  tail: readonly T[],
): TailUpdate<T> {
  if (!tail.length) return { kind: "none" };
  const held = new Set(shown.map((item) => item.id));
  const fresh = tail.filter((item) => !held.has(item.id));
  if (shown.length && fresh.length === tail.length) return { kind: "gap" };
  return { kind: "append", fresh };
}
