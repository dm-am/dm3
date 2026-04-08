import { computed, type ComputedRef } from "vue";
import { useRoute, useRouter } from "vue-router";
import type { LocationQuery } from "vue-router";
import { usePaging, createFilterDispatcher } from "@/shared/lib/composables";
import {
  parseStringFromUrl,
  parseDateFromUrl,
  parseSetFromUrl,
  parseSortDirection,
  validateSortField,
  parseNumberFromUrl,
  formatSetForUrl,
  dateToApiStart,
  dateToApiEnd,
  setToArray,
  buildQuery,
} from "@/shared/lib/filters";
import type {
  BlogsFilterState,
  BlogsSearchParams,
  StatusFilter,
} from "./types";
import { SORT_OPTIONS, STATUS_OPTIONS, DEFAULT_FILTER_STATE } from "./types";
import type { BlogStatus } from "@/entities/blog";

// =============================================================================
// RETURN TYPE
// =============================================================================

export interface BlogsFilterComposable {
  /** Current filter state (computed from URL) */
  filterState: ComputedRef<BlogsFilterState>;
  /** API search parameters (computed from filterState) */
  searchParams: ComputedRef<BlogsSearchParams>;
  /** Whether any filters are active */
  hasActiveFilters: ComputedRef<boolean>;

  // Actions
  setSearch: (search: string) => void;
  setStatus: (status: StatusFilter) => void;
  clearStatus: () => void;
  addHost: (username: string) => void;
  removeHost: (username: string) => void;
  clearHosts: () => void;
  setCreatedRange: (fromUtc: string | null, toUtc: string | null) => void;
  setActivatedRange: (fromUtc: string | null, toUtc: string | null) => void;
  setClosedRange: (fromUtc: string | null, toUtc: string | null) => void;
  setSort: (sortBy: string, sortOrder?: "asc" | "desc") => void;
  toggleSortOrder: () => void;
  clearFilters: () => void;
  removeFilter: (key: string) => void;
}

// =============================================================================
// ACTION TYPES (Discriminated Union)
// =============================================================================

type BlogsFilterAction =
  | { type: "SET_SEARCH"; search: string }
  | { type: "SET_STATUS"; status: StatusFilter }
  | { type: "ADD_HOST"; username: string }
  | { type: "REMOVE_HOST"; username: string }
  | { type: "CLEAR_HOSTS" }
  | { type: "SET_CREATED_RANGE"; fromUtc: string | null; toUtc: string | null }
  | { type: "SET_ACTIVATED_RANGE"; fromUtc: string | null; toUtc: string | null }
  | { type: "SET_CLOSED_RANGE"; fromUtc: string | null; toUtc: string | null }
  | { type: "SET_SORT"; sortBy: string; sortOrder?: "asc" | "desc" }
  | { type: "TOGGLE_SORT_ORDER" }
  | { type: "REMOVE_FILTER"; key: string }
  | { type: "CLEAR_FILTERS" };

// =============================================================================
// PURE REDUCER (Testable, no side effects)
// =============================================================================

/**
 * Pure reducer function - applies action to state and returns new state.
 * This is the ONLY place where state transformation logic lives.
 */
function reducer(state: BlogsFilterState, action: BlogsFilterAction): BlogsFilterState {
  // Clone state with new Set instance to avoid mutation
  const newState: BlogsFilterState = {
    ...state,
    hostUsernames: new Set(state.hostUsernames),
  };

  switch (action.type) {
    case "SET_SEARCH":
      newState.search = action.search;
      return newState;

    case "SET_STATUS":
      newState.status = action.status;
      return newState;

    case "ADD_HOST":
      newState.hostUsernames.add(action.username);
      return newState;

    case "REMOVE_HOST":
      newState.hostUsernames.delete(action.username);
      return newState;

    case "CLEAR_HOSTS":
      newState.hostUsernames.clear();
      return newState;

    case "SET_CREATED_RANGE":
      newState.createdFromUtc = action.fromUtc;
      newState.createdToUtc = action.toUtc;
      return newState;

    case "SET_ACTIVATED_RANGE":
      newState.activatedFromUtc = action.fromUtc;
      newState.activatedToUtc = action.toUtc;
      return newState;

    case "SET_CLOSED_RANGE":
      newState.closedFromUtc = action.fromUtc;
      newState.closedToUtc = action.toUtc;
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

    case "REMOVE_FILTER": {
      const key = action.key;
      if (key === "status") {
        newState.status = "any";
      } else if (key === "hosts") {
        newState.hostUsernames.clear();
      } else if (key.startsWith("host-")) {
        const username = key.replace("host-", "");
        newState.hostUsernames.delete(username);
      } else if (key === "createdRange") {
        newState.createdFromUtc = null;
        newState.createdToUtc = null;
      } else if (key === "activatedRange") {
        newState.activatedFromUtc = null;
        newState.activatedToUtc = null;
      } else if (key === "closedRange") {
        newState.closedFromUtc = null;
        newState.closedToUtc = null;
      }
      return newState;
    }

    case "CLEAR_FILTERS":
      return createDefaultState();

    default: {
      // Exhaustiveness check - TypeScript will error if we miss a case
      const _exhaustive: never = action;
      return _exhaustive;
    }
  }
}

// =============================================================================
// VALIDATION & PARSING
// =============================================================================

const validSortByValues = new Set<string>(SORT_OPTIONS.map((o) => o.value));
const validStatusValues = new Set<string>(STATUS_OPTIONS.map((o) => o.value));

function createDefaultState(): BlogsFilterState {
  return {
    ...DEFAULT_FILTER_STATE,
    hostUsernames: new Set(),
  };
}

function parseQueryToState(query: LocationQuery): BlogsFilterState {
  const state = createDefaultState();

  if (query.search) {
    state.search = String(query.search).slice(0, 200);
  }

  if (query.status) {
    const status = String(query.status);
    if (validStatusValues.has(status)) {
      state.status = status as StatusFilter;
    }
  }

  if (query.hosts) {
    for (const host of String(query.hosts).split(",")) {
      const trimmed = host.trim().slice(0, 20);
      if (trimmed) state.hostUsernames.add(trimmed);
    }
  }

  if (query.createdFromUtc) state.createdFromUtc = String(query.createdFromUtc);
  if (query.createdToUtc) state.createdToUtc = String(query.createdToUtc);
  if (query.activatedFromUtc) state.activatedFromUtc = String(query.activatedFromUtc);
  if (query.activatedToUtc) state.activatedToUtc = String(query.activatedToUtc);
  if (query.closedFromUtc) state.closedFromUtc = String(query.closedFromUtc);
  if (query.closedToUtc) state.closedToUtc = String(query.closedToUtc);

  const sortByRaw = query.sortBy as string;
  if (validSortByValues.has(sortByRaw)) state.sortBy = sortByRaw;

  const sortOrderRaw = query.sortOrder as string;
  if (sortOrderRaw === "asc" || sortOrderRaw === "desc") state.sortOrder = sortOrderRaw;

  return state;
}

function buildQueryFromState(state: BlogsFilterState): Record<string, string> {
  const query: Record<string, string> = {};

  if (state.search) query.search = state.search;
  if (state.status !== "any") query.status = state.status;
  if (state.hostUsernames.size > 0) query.hosts = [...state.hostUsernames].join(",");
  if (state.createdFromUtc) query.createdFromUtc = state.createdFromUtc;
  if (state.createdToUtc) query.createdToUtc = state.createdToUtc;
  if (state.activatedFromUtc) query.activatedFromUtc = state.activatedFromUtc;
  if (state.activatedToUtc) query.activatedToUtc = state.activatedToUtc;
  if (state.closedFromUtc) query.closedFromUtc = state.closedFromUtc;
  if (state.closedToUtc) query.closedToUtc = state.closedToUtc;
  if (state.sortBy !== "created") query.sortBy = state.sortBy;
  if (state.sortOrder !== "desc") query.sortOrder = state.sortOrder;

  return query;
}

// =============================================================================
// MODULE-LEVEL DISPATCHER (created once per module)
// =============================================================================

const dispatcher = createFilterDispatcher<BlogsFilterState, BlogsFilterAction>({
  buildQuery: buildQueryFromState,
  name: "useBlogsFilter",
});

// =============================================================================
// COMPOSABLE
// =============================================================================

/**
 * Blogs filter composable with action-based state management.
 *
 * Architecture:
 * - URL is the single source of truth (filterState is computed from route.query)
 * - All mutations go through dispatch() which applies actions via pure reducer
 * - Actions batch via module-level debounce - rapid clicks merge into single navigation
 * - Pure reducer is testable in isolation without router
 */
export function useBlogsFilter(): BlogsFilterComposable {
  const route = useRoute();
  const router = useRouter();
  const { entitiesPerPage } = usePaging();

  // Set router for dispatcher
  dispatcher.setRouter(router);

  // Filter state derived from URL (single source of truth)
  const filterState = computed<BlogsFilterState>(() => parseQueryToState(route.query));

  // Helper to get current state for dispatch
  const getCurrentState = () => filterState.value;

  // Dispatch helper
  const dispatch = (action: BlogsFilterAction) =>
    dispatcher.dispatch(action, reducer, getCurrentState);

  // Convert to API params
  const searchParams = computed<BlogsSearchParams>(() => {
    const state = filterState.value;
    const params: BlogsSearchParams = {};

    if (state.search) params.search = state.search;
    if (state.status !== "any") params.status = state.status as BlogStatus;
    if (state.hostUsernames.size > 0) params.hostUsernames = [...state.hostUsernames];

    if (state.createdFromUtc) params.createdFromUtc = dateToApiStart(state.createdFromUtc);
    if (state.createdToUtc) params.createdToUtc = dateToApiEnd(state.createdToUtc);
    if (state.activatedFromUtc) params.activatedFromUtc = dateToApiStart(state.activatedFromUtc);
    if (state.activatedToUtc) params.activatedToUtc = dateToApiEnd(state.activatedToUtc);
    if (state.closedFromUtc) params.closedFromUtc = dateToApiStart(state.closedFromUtc);
    if (state.closedToUtc) params.closedToUtc = dateToApiEnd(state.closedToUtc);

    params.sortBy = state.sortBy;
    params.sortOrder = state.sortOrder;

    const numberParam = route.query.number;
    if (numberParam) {
      const num = parseInt(String(numberParam), 10);
      if (!isNaN(num) && num > 0) params.number = num;
    }

    params.size = entitiesPerPage.value;
    return params;
  });

  // Active filters check
  const hasActiveFilters = computed(() => {
    const state = filterState.value;
    return (
      state.search !== "" ||
      state.status !== "any" ||
      state.hostUsernames.size > 0 ||
      state.createdFromUtc !== null ||
      state.createdToUtc !== null ||
      state.activatedFromUtc !== null ||
      state.activatedToUtc !== null ||
      state.closedFromUtc !== null ||
      state.closedToUtc !== null
    );
  });

  // ==========================================================================
  // ACTION DISPATCHERS (thin wrappers around dispatch)
  // ==========================================================================

  const setSearch = (search: string) => dispatch({ type: "SET_SEARCH", search });
  const setStatus = (status: StatusFilter) => dispatch({ type: "SET_STATUS", status });
  const clearStatus = () => dispatch({ type: "SET_STATUS", status: "any" });
  const addHost = (username: string) => dispatch({ type: "ADD_HOST", username });
  const removeHost = (username: string) => dispatch({ type: "REMOVE_HOST", username });
  const clearHosts = () => dispatch({ type: "CLEAR_HOSTS" });
  const setCreatedRange = (fromUtc: string | null, toUtc: string | null) => dispatch({ type: "SET_CREATED_RANGE", fromUtc, toUtc });
  const setActivatedRange = (fromUtc: string | null, toUtc: string | null) => dispatch({ type: "SET_ACTIVATED_RANGE", fromUtc, toUtc });
  const setClosedRange = (fromUtc: string | null, toUtc: string | null) => dispatch({ type: "SET_CLOSED_RANGE", fromUtc, toUtc });
  const setSort = (sortBy: string, sortOrder?: "asc" | "desc") => dispatch({ type: "SET_SORT", sortBy, sortOrder });
  const toggleSortOrder = () => dispatch({ type: "TOGGLE_SORT_ORDER" });
  const removeFilter = (key: string) => dispatch({ type: "REMOVE_FILTER", key });
  const clearFilters = () => dispatch({ type: "CLEAR_FILTERS" });

  return {
    filterState,
    searchParams,
    hasActiveFilters,
    setSearch,
    setStatus,
    clearStatus,
    addHost,
    removeHost,
    clearHosts,
    setCreatedRange,
    setActivatedRange,
    setClosedRange,
    setSort,
    toggleSortOrder,
    clearFilters,
    removeFilter,
  };
}
