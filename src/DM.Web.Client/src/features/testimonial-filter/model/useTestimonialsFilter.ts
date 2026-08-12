import { computed, type ComputedRef } from "vue";
import { useRoute, useRouter } from "vue-router";
import type { LocationQuery } from "vue-router";
import { createFilterDispatcher } from "@/shared/lib/composables/createFilterDispatcher";
import { parseSortDirection, validateSortField } from "@/shared/lib/filters";
import type {
  TestimonialsFilterState,
  TestimonialsSearchParams,
  TestimonialSortBy,
} from "./types";
import { DEFAULT_FILTER_STATE, SORT_OPTIONS } from "./types";

// =============================================================================
// RETURN TYPE
// =============================================================================

export interface TestimonialsFilterComposable {
  /** Current filter state (computed from URL) */
  filterState: ComputedRef<TestimonialsFilterState>;
  /** API search parameters (computed from filterState) */
  searchParams: ComputedRef<TestimonialsSearchParams>;
  /** Whether any filters are active */
  hasActiveFilters: ComputedRef<boolean>;

  // Actions
  setSearch: (search: string) => void;
  setSort: (sortBy: TestimonialSortBy, sortOrder?: "asc" | "desc") => void;
  toggleSortOrder: () => void;
  clearFilters: () => void;
}

// =============================================================================
// ACTION TYPES (Discriminated Union)
// =============================================================================

type TestimonialsFilterAction =
  | { type: "SET_SEARCH"; search: string }
  | { type: "SET_SORT"; sortBy: TestimonialSortBy; sortOrder?: "asc" | "desc" }
  | { type: "TOGGLE_SORT_ORDER" }
  | { type: "CLEAR_FILTERS" };

// =============================================================================
// PURE REDUCER (Testable, no side effects)
// =============================================================================

function reducer(
  state: TestimonialsFilterState,
  action: TestimonialsFilterAction,
): TestimonialsFilterState {
  const newState: TestimonialsFilterState = { ...state };

  switch (action.type) {
    case "SET_SEARCH":
      newState.search = action.search;
      return newState;

    case "SET_SORT": {
      newState.sortBy = action.sortBy;
      if (action.sortOrder) {
        newState.sortOrder = action.sortOrder;
      } else {
        const option = SORT_OPTIONS.find((o) => o.value === action.sortBy);
        newState.sortOrder = option?.defaultDirection || "desc";
      }
      return newState;
    }

    case "TOGGLE_SORT_ORDER":
      newState.sortOrder = newState.sortOrder === "asc" ? "desc" : "asc";
      return newState;

    case "CLEAR_FILTERS":
      return createDefaultState();

    default: {
      const _exhaustive: never = action;
      return _exhaustive;
    }
  }
}

// =============================================================================
// VALIDATION & PARSING
// =============================================================================

const validSortByValues = SORT_OPTIONS.map((o) => o.value);

function createDefaultState(): TestimonialsFilterState {
  return { ...DEFAULT_FILTER_STATE };
}

function parseQueryToState(query: LocationQuery): TestimonialsFilterState {
  const state = createDefaultState();

  if (query.search) {
    state.search = String(query.search).slice(0, 200);
  }

  state.sortBy = validateSortField(
    query.sortBy as string,
    validSortByValues,
    DEFAULT_FILTER_STATE.sortBy,
  ) as TestimonialSortBy;

  state.sortOrder = parseSortDirection(
    query.sortOrder as string,
    DEFAULT_FILTER_STATE.sortOrder,
  );

  return state;
}

function buildQueryFromState(
  state: TestimonialsFilterState,
): Record<string, string> {
  const query: Record<string, string> = {};
  const def = DEFAULT_FILTER_STATE;

  if (state.search) query.search = state.search;
  if (state.sortBy !== def.sortBy) query.sortBy = state.sortBy;
  if (state.sortOrder !== def.sortOrder) query.sortOrder = state.sortOrder;

  return query;
}

// =============================================================================
// MODULE-LEVEL DISPATCHER (created once per module)
// =============================================================================

const dispatcher = createFilterDispatcher<
  TestimonialsFilterState,
  TestimonialsFilterAction
>({
  buildQuery: buildQueryFromState,
  name: "useTestimonialsFilter",
});

// =============================================================================
// COMPOSABLE
// =============================================================================

/**
 * Testimonials filter composable with action-based state management.
 *
 * Architecture:
 * - URL is the single source of truth (filterState is computed from route.query)
 * - All mutations go through dispatch() which applies actions via pure reducer
 * - Actions batch via module-level debounce - rapid clicks merge into single navigation
 * - Pure reducer is testable in isolation without router
 */
export function useTestimonialsFilter(): TestimonialsFilterComposable {
  const route = useRoute();
  const router = useRouter();

  // Set router for dispatcher
  dispatcher.setRouter(router);

  // Filter state derived from URL (single source of truth)
  const filterState = computed<TestimonialsFilterState>(() =>
    parseQueryToState(route.query),
  );

  // Helper to get current state for dispatch
  const getCurrentState = () => filterState.value;

  // Dispatch helper
  const dispatch = (action: TestimonialsFilterAction) =>
    dispatcher.dispatch(action, reducer, getCurrentState);

  // Convert to API params
  const searchParams = computed<TestimonialsSearchParams>(() => {
    const state = filterState.value;
    const params: TestimonialsSearchParams = {};

    if (state.search) params.search = state.search;
    params.sortBy = state.sortBy;
    params.sortOrder = state.sortOrder;

    // Page number (from URL query directly)
    const numberParam = route.query.number;
    if (numberParam) {
      const num = parseInt(String(numberParam), 10);
      if (!isNaN(num) && num > 0) params.number = num;
    }

    params.size = 10; // Testimonials use fixed page size
    return params;
  });

  // Active filters check
  const hasActiveFilters = computed(() => {
    const state = filterState.value;
    const def = DEFAULT_FILTER_STATE;
    return state.search !== def.search;
  });

  // ==========================================================================
  // ACTION DISPATCHERS (thin wrappers around dispatch)
  // ==========================================================================

  const setSearch = (search: string) =>
    dispatch({ type: "SET_SEARCH", search });
  const setSort = (sortBy: TestimonialSortBy, sortOrder?: "asc" | "desc") =>
    dispatch({ type: "SET_SORT", sortBy, sortOrder });
  const toggleSortOrder = () => dispatch({ type: "TOGGLE_SORT_ORDER" });
  const clearFilters = () => dispatch({ type: "CLEAR_FILTERS" });

  return {
    filterState,
    searchParams,
    hasActiveFilters,
    setSearch,
    setSort,
    toggleSortOrder,
    clearFilters,
  };
}
