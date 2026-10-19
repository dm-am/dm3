import type { Router } from "vue-router";

export interface FilterDispatcherConfig<State> {
  /** Build URL query from state (only non-default values) */
  buildQuery: (state: State) => Record<string, string>;
  /** Module name for error logging */
  name: string;
}

/** Batches rapid actions into a single navigation. */
export const DEBOUNCE_MS = 50;

/**
 * Dispatch for URL-synced filters, created once per filter module at module
 * level: actions apply to the pending state at once, and the address is
 * written after a debounce; filterState follows the address.
 *
 * @example
 * ```typescript
 * // At module level (not inside composable!)
 * const dispatcher = createFilterDispatcher<GamesFilterState, GamesFilterAction>({
 *   buildQuery: buildQueryFromState,
 *   name: "useGamesFilter",
 * });
 *
 * // Inside composable
 * export function useGamesFilter() {
 *   dispatcher.setRouter(useRouter());
 *   const setSearch = (search: string) =>
 *     dispatcher.dispatch({ type: "SET_SEARCH", search }, reducer, getCurrentState);
 * }
 * ```
 */
export function createFilterDispatcher<State, Action>(
  config: FilterDispatcherConfig<State>,
) {
  let debounceTimer: ReturnType<typeof setTimeout> | null = null;
  let pendingState: State | null = null;
  let isNavigating = false;
  let routerInstance: Router | null = null;

  /** Called by the composable on each use. */
  function setRouter(router: Router): void {
    routerInstance = router;
  }

  /** The only way to modify filter state. */
  function dispatch(
    action: Action,
    reducer: (state: State, action: Action) => State,
    getCurrentState: () => State,
  ): void {
    const baseState = pendingState ?? getCurrentState();
    const newState = reducer(baseState, action);

    // The reducer returns the same reference when nothing changed.
    if (newState === baseState) return;

    pendingState = newState;

    // The dispatcher outlives the page: by the time the timer fires the reader
    // may be elsewhere, and replace({ name, query }) would stamp this query on
    // that page. The route name is shared by every board, so the path decides.
    const scheduledPath = routerInstance?.currentRoute.value.path ?? null;

    if (debounceTimer) clearTimeout(debounceTimer);

    const applyPendingState = async () => {
      debounceTimer = null;

      if (!pendingState || !routerInstance) return;
      // Re-armed rather than dropped, or the change would be lost.
      if (isNavigating) {
        debounceTimer = setTimeout(applyPendingState, DEBOUNCE_MS);
        return;
      }

      const currentRoute = routerInstance.currentRoute.value;

      // Our own replace only changes the query, so this is a page left.
      if (scheduledPath !== null && currentRoute.path !== scheduledPath) {
        pendingState = null;
        return;
      }

      const query = config.buildQuery(pendingState);
      pendingState = null;

      const currentQuery = currentRoute.query;

      const queryKeys = Object.keys(query);
      const currentKeys = Object.keys(currentQuery).filter(
        (k) => currentQuery[k] !== undefined && currentQuery[k] !== "",
      );

      if (queryKeys.length === currentKeys.length) {
        let same = true;
        for (const key of queryKeys) {
          if (query[key] !== currentQuery[key]) {
            same = false;
            break;
          }
        }
        if (same) return;
      }

      // By name, not path, so a query-only change keeps the component.
      isNavigating = true;
      try {
        await routerInstance.replace({
          name: currentRoute.name as string,
          query,
        });
      } catch (err: unknown) {
        const name = err instanceof Error ? err.name : undefined;
        if (name !== "NavigationDuplicated" && name !== "NavigationCancelled") {
          console.error(`[${config.name}] Navigation error:`, err);
        }
      } finally {
        isNavigating = false;
      }
    };

    debounceTimer = setTimeout(applyPendingState, DEBOUNCE_MS);
  }

  return {
    setRouter,
    dispatch,
  };
}
