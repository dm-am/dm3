/**
 * Monotonically increasing request counter used to discard stale async
 * responses (out-of-order network replies racing a newer request).
 *
 * One guard shared by every search store instead of a counter per store: a store
 * that skips the check renders whichever reply happens to land last.
 *
 * @example
 * ```ts
 * const guard = createRequestGuard();
 *
 * async function search(params: Params) {
 *   const requestId = guard.next();
 *   const { data } = await api.search(params);
 *   if (!guard.isCurrent(requestId)) return; // a newer request won the race
 *   result.value = data;
 * }
 * ```
 *
 * `current()` is the same guard read from the other side, and it is what a
 * store needs when the thing being superseded is not the request but what the
 * request describes. The global chat holds a window of messages; every
 * wholesale replacement of that window (a fresh load, a jump to a message or a
 * date) invalidates whatever catch-up read is in flight, because its answer
 * describes a window nobody is looking at any more. The replacements bump the
 * guard, and the reader takes the number as it stands instead of claiming a
 * request of its own:
 *
 * @example
 * ```ts
 * const windowGuard = createRequestGuard();
 *
 * function replaceWindow(page: Page) {
 *   windowGuard.next();
 *   messages.value = page.resources;
 * }
 *
 * async function catchUp() {
 *   const heldWindow = windowGuard.current();
 *   const { data } = await api.getLatest();
 *   if (!windowGuard.isCurrent(heldWindow)) return; // the window was replaced
 *   append(data);
 * }
 * ```
 */
export function createRequestGuard(): {
  next(): number;
  current(): number;
  isCurrent(id: number): boolean;
} {
  let currentId = 0;

  return {
    next(): number {
      return ++currentId;
    },
    current(): number {
      return currentId;
    },
    isCurrent(id: number): boolean {
      return id === currentId;
    },
  };
}
