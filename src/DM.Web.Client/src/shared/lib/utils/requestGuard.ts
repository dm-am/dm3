/**
 * A request counter that discards stale async responses.
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
 * `current()` reads the guard without claiming a request: for a read that a
 * replacement of what it describes supersedes.
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
