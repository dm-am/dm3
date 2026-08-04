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
 */
export function createRequestGuard(): {
  next(): number;
  isCurrent(id: number): boolean;
} {
  let currentId = 0;

  return {
    next(): number {
      return ++currentId;
    },
    isCurrent(id: number): boolean {
      return id === currentId;
    },
  };
}
