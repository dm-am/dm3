import type { Router } from "vue-router";

/**
 * Configuration for filter dispatcher
 */
export interface FilterDispatcherConfig<State> {
  /** Build URL query from state (only non-default values) */
  buildQuery: (state: State) => Record<string, string>;
  /** Module name for error logging */
  name: string;
}

/**
 * Dispatch infrastructure for URL-synced filters.
 *
 * This is a factory that creates module-level state for each filter type.
 * Call it once per filter module (at module level, not inside composable).
 *
 * Architecture:
 * - Actions are applied immediately to pendingState via reducer
 * - URL update is debounced - rapid actions batch into single navigation
 * - Once URL updates, filterState computed refreshes automatically
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
  /** Debounce delay for URL updates. Batches rapid clicks into single navigation. */
  const DEBOUNCE_MS = 50;

  // Module-level state (created once per filter type)
  let debounceTimer: ReturnType<typeof setTimeout> | null = null;
  let pendingState: State | null = null;
  let isNavigating = false;
  let routerInstance: Router | null = null;

  /**
   * Set router instance. Must be called from composable on each use.
   */
  function setRouter(router: Router): void {
    routerInstance = router;
  }

  /**
   * Get current pending state (for stacking rapid actions)
   */
  function getPendingState(): State | null {
    return pendingState;
  }

  /**
   * Dispatch an action - applies it to pending state and schedules URL update.
   * This is the ONLY way to modify filter state.
   */
  function dispatch(
    action: Action,
    reducer: (state: State, action: Action) => State,
    getCurrentState: () => State,
  ): void {
    // Apply action to pending state (or current state if no pending)
    const baseState = pendingState ?? getCurrentState();
    const newState = reducer(baseState, action);

    // Reducer can return same reference if nothing changed (optimization)
    if (newState === baseState) return;

    pendingState = newState;

    // Clear existing debounce timer
    if (debounceTimer) clearTimeout(debounceTimer);

    // Schedule URL update
    debounceTimer = setTimeout(async () => {
      debounceTimer = null;

      if (!pendingState || !routerInstance) return;
      if (isNavigating) return;

      const query = config.buildQuery(pendingState);
      pendingState = null;

      // Use synchronous access to current route (avoid stale closure)
      const currentRoute = routerInstance.currentRoute.value;
      const currentQuery = currentRoute.query;

      // Skip if query unchanged
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

      // Use route NAME (not path) to ensure Vue Router recognizes same route
      // This prevents component recreation during query-only changes
      isNavigating = true;
      try {
        await routerInstance.replace({
          name: currentRoute.name as string,
          query,
        });
      } catch (err: unknown) {
        const error = err as { name?: string };
        if (
          error?.name !== "NavigationDuplicated" &&
          error?.name !== "NavigationCancelled"
        ) {
          console.error(`[${config.name}] Navigation error:`, err);
        }
      } finally {
        isNavigating = false;
      }
    }, DEBOUNCE_MS);
  }

  return {
    setRouter,
    dispatch,
    getPendingState,
  };
}

export type FilterDispatcher<State, Action> = ReturnType<
  typeof createFilterDispatcher<State, Action>
>;
