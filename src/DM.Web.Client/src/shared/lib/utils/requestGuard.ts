/**
 * Monotonically increasing request counter used to discard stale async
 * responses (out-of-order network replies racing a newer request).
 *
 * The blogs store pioneered this pattern inline (`currentRequestId`); this
 * is the SSOT extraction so every search store shares the same guard logic
 * instead of copy-pasting the counter.
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
