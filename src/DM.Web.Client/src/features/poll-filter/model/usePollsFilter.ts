import { computed, type ComputedRef } from "vue";
import { useRoute, useRouter } from "vue-router";
import type { LocationQuery } from "vue-router";
import { usePaging } from "@/shared/lib/composables/usePaging";
import { createFilterDispatcher } from "@/shared/lib/composables/createFilterDispatcher";
import {
  applySortAction,
  parseSortDirection,
  validateSortField,
} from "@/shared/lib/filters";
import type {
  PollSortBy,
  PollStatus,
  PollsSearchParams,
} from "@/entities/poll";
import type { PollsFilterState } from "./types";
import {
  DEFAULT_FILTER_STATE,
  SORT_OPTIONS,
  STATUS_OPTIONS,
  POLL_TYPE_OPTIONS,
} from "./types";

// =============================================================================
// RETURN TYPE
// =============================================================================

export interface PollsFilterComposable {
  /** Current filter state (computed from URL) */
  filterState: ComputedRef<PollsFilterState>;
  /** API search parameters (computed from filterState) */
  searchParams: ComputedRef<PollsSearchParams>;
  /** Whether any filters are active */
  hasActiveFilters: ComputedRef<boolean>;

  // Actions
  setStatus: (status: PollStatus | "") => void;
  setPollType: (pollType: "anonymous" | "public" | "") => void;
  setSearch: (search: string) => void;
  setStartsFromUtc: (date: string) => void;
  setStartsToUtc: (date: string) => void;
  setEndsFromUtc: (date: string) => void;
  setEndsToUtc: (date: string) => void;
  setSort: (sortBy: PollSortBy, sortOrder?: "asc" | "desc") => void;
  toggleSortOrder: () => void;
  clearFilters: () => void;
}

// =============================================================================
// ACTION TYPES (Discriminated Union)
// =============================================================================

type PollsFilterAction =
  | { type: "SET_STATUS"; status: PollStatus | "" }
  | { type: "SET_POLL_TYPE"; pollType: "anonymous" | "public" | "" }
  | { type: "SET_SEARCH"; search: string }
  | { type: "SET_STARTS_FROM"; date: string }
  | { type: "SET_STARTS_TO"; date: string }
  | { type: "SET_ENDS_FROM"; date: string }
  | { type: "SET_ENDS_TO"; date: string }
  | { type: "SET_SORT"; sortBy: PollSortBy; sortOrder?: "asc" | "desc" }
  | { type: "TOGGLE_SORT_ORDER" }
  | { type: "CLEAR_FILTERS" };

// =============================================================================
// PURE REDUCER (Testable, no side effects)
// =============================================================================

function reducer(
  state: PollsFilterState,
  action: PollsFilterAction,
): PollsFilterState {
  const newState: PollsFilterState = { ...state };

  switch (action.type) {
    case "SET_STATUS":
      newState.status = action.status;
      return newState;

    case "SET_POLL_TYPE":
      newState.pollType = action.pollType;
      return newState;

    case "SET_SEARCH":
      newState.search = action.search;
      return newState;

    case "SET_STARTS_FROM":
      newState.startsFromUtc = action.date;
      return newState;

    case "SET_STARTS_TO":
      newState.startsToUtc = action.date;
      return newState;

    case "SET_ENDS_FROM":
      newState.endsFromUtc = action.date;
      return newState;

    case "SET_ENDS_TO":
      newState.endsToUtc = action.date;
      return newState;

    case "SET_SORT":
    case "TOGGLE_SORT_ORDER":
      applySortAction(newState, action, SORT_OPTIONS);
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
const validStatusValues = STATUS_OPTIONS.map((o) => o.value);
const validPollTypeValues = POLL_TYPE_OPTIONS.map((o) => o.value);

function createDefaultState(): PollsFilterState {
  return { ...DEFAULT_FILTER_STATE };
}

function parseQueryToState(query: LocationQuery): PollsFilterState {
  const state = createDefaultState();

  // Parse status
  if (query.status) {
    const statusStr = String(query.status);
    if (validStatusValues.includes(statusStr as any)) {
      state.status = statusStr as PollStatus | "";
    }
  }

  // Parse poll type
  if (query.pollType) {
    const pollTypeStr = String(query.pollType);
    if (validPollTypeValues.includes(pollTypeStr as any)) {
      state.pollType = pollTypeStr as "anonymous" | "public" | "";
    }
  }

  // Parse search
  if (query.search) {
    state.search = String(query.search).slice(0, 200);
  }

  // Parse date filters (ISO date strings)
  if (query.startsFromUtc) state.startsFromUtc = String(query.startsFromUtc);
  if (query.startsToUtc) state.startsToUtc = String(query.startsToUtc);
  if (query.endsFromUtc) state.endsFromUtc = String(query.endsFromUtc);
  if (query.endsToUtc) state.endsToUtc = String(query.endsToUtc);

  // Parse sort
  state.sortBy = validateSortField(
    query.sortBy as string,
    validSortByValues,
    DEFAULT_FILTER_STATE.sortBy,
  ) as PollSortBy;

  state.sortOrder = parseSortDirection(
    query.sortOrder as string,
    DEFAULT_FILTER_STATE.sortOrder,
  );

  return state;
}

function buildQueryFromState(state: PollsFilterState): Record<string, string> {
  const query: Record<string, string> = {};
  const def = DEFAULT_FILTER_STATE;

  if (state.status) query.status = state.status;
  if (state.pollType) query.pollType = state.pollType;
  if (state.search) query.search = state.search;
  if (state.startsFromUtc) query.startsFromUtc = state.startsFromUtc;
  if (state.startsToUtc) query.startsToUtc = state.startsToUtc;
  if (state.endsFromUtc) query.endsFromUtc = state.endsFromUtc;
  if (state.endsToUtc) query.endsToUtc = state.endsToUtc;
  if (state.sortBy !== def.sortBy) query.sortBy = state.sortBy;
  if (state.sortOrder !== def.sortOrder) query.sortOrder = state.sortOrder;

  return query;
}

// =============================================================================
// MODULE-LEVEL DISPATCHER (created once per module)
// =============================================================================

const dispatcher = createFilterDispatcher<PollsFilterState, PollsFilterAction>({
  buildQuery: buildQueryFromState,
  name: "usePollsFilter",
});

// =============================================================================
// COMPOSABLE
// =============================================================================

/**
 * Polls filter composable with action-based state management.
 *
 * Architecture:
 * - URL is the single source of truth (filterState is computed from route.query)
 * - All mutations go through dispatch() which applies actions via pure reducer
 * - Actions batch via module-level debounce - rapid clicks merge into single navigation
 * - Pure reducer is testable in isolation without router
 */
export function usePollsFilter(): PollsFilterComposable {
  const route = useRoute();
  const router = useRouter();
  const { pollsPerPage } = usePaging();

  // Set router for dispatcher
  dispatcher.setRouter(router);

  // Filter state derived from URL (single source of truth)
  const filterState = computed<PollsFilterState>(() =>
    parseQueryToState(route.query),
  );

  // Helper to get current state for dispatch
  const getCurrentState = () => filterState.value;

  // Dispatch helper
  const dispatch = (action: PollsFilterAction) =>
    dispatcher.dispatch(action, reducer, getCurrentState);

  // Convert to API params
  const searchParams = computed<PollsSearchParams>(() => {
    const state = filterState.value;
    const params: PollsSearchParams = {};

    if (state.status) params.status = state.status;
    if (state.pollType === "anonymous") params.isAnonymous = true;
    else if (state.pollType === "public") params.isAnonymous = false;
    if (state.search) params.search = state.search;
    if (state.startsFromUtc) params.startsFromUtc = state.startsFromUtc;
    if (state.startsToUtc) params.startsToUtc = state.startsToUtc;
    if (state.endsFromUtc) params.endsFromUtc = state.endsFromUtc;
    if (state.endsToUtc) params.endsToUtc = state.endsToUtc;
    params.sortBy = state.sortBy;
    params.sortOrder = state.sortOrder;

    // Page number (from URL query directly)
    const numberParam = route.query.number;
    if (numberParam) {
      const num = parseInt(String(numberParam), 10);
      if (!isNaN(num) && num > 0) params.number = num;
    }

    params.size = pollsPerPage.value;
    return params;
  });

  // Active filters check
  const hasActiveFilters = computed(() => {
    const state = filterState.value;
    const def = DEFAULT_FILTER_STATE;
    return (
      state.status !== def.status ||
      state.pollType !== def.pollType ||
      state.search !== def.search ||
      state.startsFromUtc !== def.startsFromUtc ||
      state.startsToUtc !== def.startsToUtc ||
      state.endsFromUtc !== def.endsFromUtc ||
      state.endsToUtc !== def.endsToUtc
    );
  });

  // ==========================================================================
  // ACTION DISPATCHERS (thin wrappers around dispatch)
  // ==========================================================================

  const setStatus = (status: PollStatus | "") =>
    dispatch({ type: "SET_STATUS", status });
  const setPollType = (pollType: "anonymous" | "public" | "") =>
    dispatch({ type: "SET_POLL_TYPE", pollType });
  const setSearch = (search: string) =>
    dispatch({ type: "SET_SEARCH", search });
  const setStartsFromUtc = (date: string) =>
    dispatch({ type: "SET_STARTS_FROM", date });
  const setStartsToUtc = (date: string) =>
    dispatch({ type: "SET_STARTS_TO", date });
  const setEndsFromUtc = (date: string) =>
    dispatch({ type: "SET_ENDS_FROM", date });
  const setEndsToUtc = (date: string) =>
    dispatch({ type: "SET_ENDS_TO", date });
  const setSort = (sortBy: PollSortBy, sortOrder?: "asc" | "desc") =>
    dispatch({ type: "SET_SORT", sortBy, sortOrder });
  const toggleSortOrder = () => dispatch({ type: "TOGGLE_SORT_ORDER" });
  const clearFilters = () => dispatch({ type: "CLEAR_FILTERS" });

  return {
    filterState,
    searchParams,
    hasActiveFilters,
    setStatus,
    setPollType,
    setSearch,
    setStartsFromUtc,
    setStartsToUtc,
    setEndsFromUtc,
    setEndsToUtc,
    setSort,
    toggleSortOrder,
    clearFilters,
  };
}
