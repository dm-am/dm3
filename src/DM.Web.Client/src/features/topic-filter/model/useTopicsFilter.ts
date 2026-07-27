import { computed, type ComputedRef } from "vue";
import { useRoute, useRouter } from "vue-router";
import type { LocationQuery } from "vue-router";
import { usePaging, createFilterDispatcher } from "@/shared/lib/composables";
import {
  parseStringFromUrl,
  parseDateFromUrl,
  parseSortDirection,
  validateSortField,
  dateToApiStart,
  dateToApiEnd,
} from "@/shared/lib/filters";
import type {
  TopicsFilterState,
  TopicsSearchParams,
  SortByValue,
} from "./types";
import { SORT_OPTIONS } from "./types";

// =============================================================================
// RETURN TYPE
// =============================================================================

export interface TopicsFilterComposable {
  /** Current filter state (computed from URL) */
  filterState: ComputedRef<TopicsFilterState>;
  /** API search parameters (computed from filterState) */
  searchParams: ComputedRef<TopicsSearchParams>;
  /** Whether any filters are active */
  hasActiveFilters: ComputedRef<boolean>;

  // Actions
  setSearch: (search: string) => void;
  addAuthor: (username: string) => void;
  removeAuthor: (username: string) => void;
  clearAuthors: () => void;
  setDateRange: (from: string | null, to: string | null) => void;
  setSort: (sortBy: SortByValue, sortOrder?: "asc" | "desc") => void;
  toggleSortOrder: () => void;
  clearFilters: () => void;
}

// =============================================================================
// ACTION TYPES (Discriminated Union)
// =============================================================================

type TopicsFilterAction =
  | { type: "SET_SEARCH"; search: string }
  | { type: "ADD_AUTHOR"; username: string }
  | { type: "REMOVE_AUTHOR"; username: string }
  | { type: "CLEAR_AUTHORS" }
  | { type: "SET_DATE_RANGE"; from: string | null; to: string | null }
  | { type: "SET_SORT"; sortBy: SortByValue; sortOrder?: "asc" | "desc" }
  | { type: "TOGGLE_SORT_ORDER" }
  | { type: "CLEAR_FILTERS" };

// =============================================================================
// PURE REDUCER
// =============================================================================

function reducer(
  state: TopicsFilterState,
  action: TopicsFilterAction,
): TopicsFilterState {
  // Clone state with new Set to avoid mutation
  const newState: TopicsFilterState = {
    ...state,
    authors: new Set(state.authors),
  };

  switch (action.type) {
    case "SET_SEARCH":
      newState.search = action.search;
      return newState;

    case "ADD_AUTHOR":
      newState.authors.add(action.username);
      return newState;

    case "REMOVE_AUTHOR":
      newState.authors.delete(action.username);
      return newState;

    case "CLEAR_AUTHORS":
      newState.authors.clear();
      return newState;

    case "SET_DATE_RANGE":
      newState.createdFromUtc = action.from;
      newState.createdToUtc = action.to;
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
// VALIDATION & PARSING (using shared utilities)
// =============================================================================

const validSortByValues = SORT_OPTIONS.map((o) => o.value);

function createDefaultState(): TopicsFilterState {
  return {
    search: "",
    authors: new Set(),
    createdFromUtc: null,
    createdToUtc: null,
    sortBy: "lastActivity",
    sortOrder: "desc",
  };
}

function parseQueryToState(query: LocationQuery): TopicsFilterState {
  const state = createDefaultState();

  state.search = parseStringFromUrl(query.search as string, 200);

  // Parse authors from comma-separated string
  if (query.authors) {
    for (const author of String(query.authors).split(",")) {
      const trimmed = author.trim().slice(0, 20);
      if (trimmed) state.authors.add(trimmed);
    }
  }

  state.createdFromUtc = parseDateFromUrl(query.createdFromUtc as string);
  state.createdToUtc = parseDateFromUrl(query.createdToUtc as string);
  state.sortBy = validateSortField(
    query.sortBy as string,
    validSortByValues,
    "lastActivity",
  ) as SortByValue;
  state.sortOrder = parseSortDirection(query.sortOrder as string, "desc");

  return state;
}

function buildQueryFromState(state: TopicsFilterState): Record<string, string> {
  const query: Record<string, string> = {};

  if (state.search) query.search = state.search;
  if (state.authors.size > 0) query.authors = [...state.authors].join(",");
  if (state.createdFromUtc) query.createdFromUtc = state.createdFromUtc;
  if (state.createdToUtc) query.createdToUtc = state.createdToUtc;
  // Sort params always in URL for bookmarkability
  if (state.sortBy !== "lastActivity") query.sortBy = state.sortBy;
  if (state.sortOrder !== "desc") query.sortOrder = state.sortOrder;

  return query;
}

// =============================================================================
// MODULE-LEVEL DISPATCHER
// =============================================================================

const dispatcher = createFilterDispatcher<
  TopicsFilterState,
  TopicsFilterAction
>({
  buildQuery: buildQueryFromState,
  name: "useTopicsFilter",
});

// =============================================================================
// COMPOSABLE
// =============================================================================

/**
 * Topics filter composable with action-based state management.
 *
 * Architecture:
 * - URL is the single source of truth (filterState is computed from route.query)
 * - All mutations go through dispatch() which applies actions via pure reducer
 * - Actions batch via module-level debounce
 */
export function useTopicsFilter(): TopicsFilterComposable {
  const route = useRoute();
  const router = useRouter();
  const { topicsPerPage } = usePaging();

  dispatcher.setRouter(router);

  const filterState = computed<TopicsFilterState>(() =>
    parseQueryToState(route.query),
  );

  const getCurrentState = () => filterState.value;

  const dispatch = (action: TopicsFilterAction) =>
    dispatcher.dispatch(action, reducer, getCurrentState);

  const searchParams = computed<TopicsSearchParams>(() => {
    const state = filterState.value;
    const params: TopicsSearchParams = {};

    if (state.search) params.search = state.search;
    if (state.authors.size > 0) params.authors = [...state.authors];

    // Convert dates to ISO 8601 with time for API
    if (state.createdFromUtc)
      params.createdFromUtc = dateToApiStart(state.createdFromUtc);
    if (state.createdToUtc)
      params.createdToUtc = dateToApiEnd(state.createdToUtc);

    params.sortBy = state.sortBy;
    params.sortOrder = state.sortOrder;

    const numberParam = route.query.number;
    if (numberParam) {
      const num = parseInt(String(numberParam), 10);
      if (!isNaN(num) && num > 0) params.number = num;
    }

    params.size = topicsPerPage.value;
    return params;
  });

  // hasActiveFilters excludes sort (consistent with Games/Blogs)
  // Sort is a view preference, not a filter that reduces results
  const hasActiveFilters = computed(() => {
    const state = filterState.value;
    return (
      state.search !== "" ||
      state.authors.size > 0 ||
      state.createdFromUtc !== null ||
      state.createdToUtc !== null
    );
  });

  // Action dispatchers
  const setSearch = (search: string) =>
    dispatch({ type: "SET_SEARCH", search });
  const addAuthor = (username: string) =>
    dispatch({ type: "ADD_AUTHOR", username });
  const removeAuthor = (username: string) =>
    dispatch({ type: "REMOVE_AUTHOR", username });
  const clearAuthors = () => dispatch({ type: "CLEAR_AUTHORS" });
  const setDateRange = (from: string | null, to: string | null) =>
    dispatch({ type: "SET_DATE_RANGE", from, to });
  const setSort = (sortBy: SortByValue, sortOrder?: "asc" | "desc") =>
    dispatch({ type: "SET_SORT", sortBy, sortOrder });
  const toggleSortOrder = () => dispatch({ type: "TOGGLE_SORT_ORDER" });
  const clearFilters = () => dispatch({ type: "CLEAR_FILTERS" });

  return {
    filterState,
    searchParams,
    hasActiveFilters,
    setSearch,
    addAuthor,
    removeAuthor,
    clearAuthors,
    setDateRange,
    setSort,
    toggleSortOrder,
    clearFilters,
  };
}
