/**
 * URL-synced filter over "search + authors + created range + sort".
 *
 * Two lists on the site read exactly that shape, and their composables had
 * grown into byte-for-byte twins of 280 lines: the same reducer, the same query
 * parser, the same builder, differing only in their default sort and in which
 * page-size preference they read. A change to the common half had to be made
 * twice, and nothing in either file pointed at the other one. Both are built
 * from here now; a third list of the same shape declares its sort options and
 * joins them.
 *
 * What stays with the caller is what actually differs between lists: the sort
 * options, the pair of defaults, and the paging preference. Everything else,
 * the module-level dispatcher included, lives here.
 */
import { computed, type ComputedRef } from "vue";
import { useRoute, useRouter, type LocationQuery } from "vue-router";
import { usePaging } from "@/shared/lib/composables/usePaging";
import { createFilterDispatcher } from "@/shared/lib/composables/createFilterDispatcher";
import type {
  BaseFilterState,
  BaseSearchParams,
  SortDirection,
  SortOption,
} from "./types";
import {
  dateToApiEnd,
  dateToApiStart,
  parseDateFromUrl,
  parseSortDirection,
  parseStringFromUrl,
  validateSortField,
} from "./utils";

/** Longest author name accepted out of the URL. */
const MAX_AUTHOR_LENGTH = 20;

/** Longest search string accepted out of the URL. */
const MAX_SEARCH_LENGTH = 200;

/**
 * Filter state held in the URL.
 *
 * Naming follows the site-wide convention: date params carry the "Utc" suffix,
 * and multi-select authors are a Set (like the hosts filter of games).
 */
export interface AuthorDateFilterState<TSortBy extends string = string>
  extends BaseFilterState {
  sortBy: TSortBy;
  authors: Set<string>;
  /** YYYY-MM-DD */
  createdFromUtc: string | null;
  /** YYYY-MM-DD */
  createdToUtc: string | null;
}

/** API search params. Dates are widened to ISO 8601 with time on the way out. */
export interface AuthorDateSearchParams extends BaseSearchParams {
  authors?: string[];
  /** ISO 8601 (YYYY-MM-DDTHH:mm:ssZ) */
  createdFromUtc?: string;
  /** ISO 8601 (YYYY-MM-DDTHH:mm:ssZ) */
  createdToUtc?: string;
}

/** The page-size preference a list reads for its size param. */
export type PagingPreference = keyof ReturnType<typeof usePaging>;

export interface AuthorDateFilterConfig<TSortBy extends string> {
  /** Module name, used by the dispatcher when it reports a navigation error. */
  name: string;
  /** Sort fields this list offers; also the whitelist for the URL value. */
  sortOptions: readonly SortOption[];
  /** Sort applied when the URL says nothing. Kept out of the URL as well. */
  defaultSortBy: TSortBy;
  /** Direction applied when the URL says nothing. */
  defaultSortOrder: SortDirection;
  /** Which of the viewer page-size preferences fills the size param. */
  pagingPreference: PagingPreference;
}

export interface AuthorDateFilter<TSortBy extends string> {
  /** Current filter state (computed from URL) */
  filterState: ComputedRef<AuthorDateFilterState<TSortBy>>;
  /** API search parameters (computed from filterState) */
  searchParams: ComputedRef<AuthorDateSearchParams>;
  /** Whether any filters are active */
  hasActiveFilters: ComputedRef<boolean>;

  // Actions
  setSearch: (search: string) => void;
  addAuthor: (username: string) => void;
  removeAuthor: (username: string) => void;
  clearAuthors: () => void;
  setDateRange: (from: string | null, to: string | null) => void;
  setSort: (sortBy: TSortBy, sortOrder?: SortDirection) => void;
  toggleSortOrder: () => void;
  clearFilters: () => void;
}

type FilterAction<TSortBy extends string> =
  | { type: "SET_SEARCH"; search: string }
  | { type: "ADD_AUTHOR"; username: string }
  | { type: "REMOVE_AUTHOR"; username: string }
  | { type: "CLEAR_AUTHORS" }
  | { type: "SET_DATE_RANGE"; from: string | null; to: string | null }
  | { type: "SET_SORT"; sortBy: TSortBy; sortOrder?: SortDirection }
  | { type: "TOGGLE_SORT_ORDER" }
  | { type: "CLEAR_FILTERS" };

/**
 * Build the composable for one list.
 *
 * Call at module level: the dispatcher it creates batches rapid actions into a
 * single navigation and has to outlive the component, exactly as it did when
 * every filter module made its own.
 */
export function createAuthorDateFilter<TSortBy extends string = string>(
  config: AuthorDateFilterConfig<TSortBy>,
): () => AuthorDateFilter<TSortBy> {
  type State = AuthorDateFilterState<TSortBy>;
  type Action = FilterAction<TSortBy>;

  const validSortByValues = config.sortOptions.map((o) => o.value);

  function createDefaultState(): State {
    return {
      search: "",
      authors: new Set(),
      createdFromUtc: null,
      createdToUtc: null,
      sortBy: config.defaultSortBy,
      sortOrder: config.defaultSortOrder,
    };
  }

  // Pure reducer: the only thing that turns an action into a new state.
  function reducer(state: State, action: Action): State {
    // Clone state with new Set to avoid mutation
    const newState: State = { ...state, authors: new Set(state.authors) };

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
          const option = config.sortOptions.find(
            (o) => o.value === action.sortBy,
          );
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

  function parseQueryToState(query: LocationQuery): State {
    const state = createDefaultState();

    state.search = parseStringFromUrl(
      query.search as string,
      MAX_SEARCH_LENGTH,
    );

    // Parse authors from comma-separated string
    if (query.authors) {
      for (const author of String(query.authors).split(",")) {
        const trimmed = author.trim().slice(0, MAX_AUTHOR_LENGTH);
        if (trimmed) state.authors.add(trimmed);
      }
    }

    state.createdFromUtc = parseDateFromUrl(query.createdFromUtc as string);
    state.createdToUtc = parseDateFromUrl(query.createdToUtc as string);
    state.sortBy = validateSortField(
      query.sortBy as string,
      validSortByValues,
      config.defaultSortBy,
    ) as TSortBy;
    state.sortOrder = parseSortDirection(
      query.sortOrder as string,
      config.defaultSortOrder,
    );

    return state;
  }

  function buildQueryFromState(state: State): Record<string, string> {
    const query: Record<string, string> = {};

    if (state.search) query.search = state.search;
    if (state.authors.size > 0) query.authors = [...state.authors].join(",");
    if (state.createdFromUtc) query.createdFromUtc = state.createdFromUtc;
    if (state.createdToUtc) query.createdToUtc = state.createdToUtc;
    // A sort that is not the default is written out so the view survives a
    // bookmark; the default stays out to keep the address clean.
    if (state.sortBy !== config.defaultSortBy) query.sortBy = state.sortBy;
    if (state.sortOrder !== config.defaultSortOrder) {
      query.sortOrder = state.sortOrder;
    }

    return query;
  }

  // Module-level dispatcher: one per filter, created with the filter itself.
  const dispatcher = createFilterDispatcher<State, Action>({
    buildQuery: buildQueryFromState,
    name: config.name,
  });

  /**
   * Filter composable with action-based state management.
   *
   * Architecture:
   * - URL is the single source of truth (filterState is computed from route.query)
   * - All mutations go through dispatch() which applies actions via pure reducer
   * - Actions batch via module-level debounce
   */
  return function useAuthorDateFilter(): AuthorDateFilter<TSortBy> {
    const route = useRoute();
    const router = useRouter();
    const pageSize = usePaging()[config.pagingPreference];

    dispatcher.setRouter(router);

    const filterState = computed<State>(() => parseQueryToState(route.query));

    const getCurrentState = () => filterState.value;

    const dispatch = (action: Action) =>
      dispatcher.dispatch(action, reducer, getCurrentState);

    const searchParams = computed<AuthorDateSearchParams>(() => {
      const state = filterState.value;
      const params: AuthorDateSearchParams = {};

      if (state.search) params.search = state.search;
      if (state.authors.size > 0) params.authors = [...state.authors];

      // Convert dates to ISO 8601 with time for API
      if (state.createdFromUtc)
        params.createdFromUtc = dateToApiStart(state.createdFromUtc);
      if (state.createdToUtc)
        params.createdToUtc = dateToApiEnd(state.createdToUtc);

      params.sortBy = state.sortBy;
      params.sortOrder = state.sortOrder;

      // Use "number" param (unified pagination)
      const numberParam = route.query.number;
      if (numberParam) {
        const num = parseInt(String(numberParam), 10);
        if (!isNaN(num) && num > 0) params.number = num;
      }

      params.size = pageSize.value;
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
    const setSort = (sortBy: TSortBy, sortOrder?: SortDirection) =>
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
  };
}
