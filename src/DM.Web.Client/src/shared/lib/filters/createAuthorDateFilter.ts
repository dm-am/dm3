/**
 * URL-synced filter over "search + authors + created range + sort" for every
 * list of that shape; the caller states its sort options and defaults.
 */
import { computed, type ComputedRef } from "vue";
import { useRoute, useRouter, type LocationQuery } from "vue-router";
import { usePaging } from "@/shared/lib/composables/usePaging";
import { createFilterDispatcher } from "@/shared/lib/composables/createFilterDispatcher";
import { reduceSortOrClear, sortOrClearDispatchers } from "./applySortAction";
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
  parsePageNumber,
  parseStringFromUrl,
  toQueryValue,
  validateSortField,
} from "./values";

/** Longest author name accepted out of the URL. */
const MAX_AUTHOR_LENGTH = 20;

/** Longest search string accepted out of the URL. */
const MAX_SEARCH_LENGTH = 200;

/** Filter state held in the URL; dates carry the "Utc" suffix. */
interface AuthorDateFilterState<
  TSortBy extends string = string,
> extends BaseFilterState {
  sortBy: TSortBy;
  authors: Set<string>;
  /** YYYY-MM-DD */
  createdFromUtc: string | null;
  /** YYYY-MM-DD */
  createdToUtc: string | null;
}

/** API search params. Dates are widened to ISO 8601 with time on the way out. */
interface AuthorDateSearchParams extends BaseSearchParams {
  authors?: string[];
  /** ISO 8601 (YYYY-MM-DDTHH:mm:ssZ) */
  createdFromUtc?: string;
  /** ISO 8601 (YYYY-MM-DDTHH:mm:ssZ) */
  createdToUtc?: string;
}

export interface AuthorDateFilterConfig<TSortBy extends string> {
  /** Module name, used by the dispatcher when it reports a navigation error. */
  name: string;
  /** Sort fields this list offers; also the whitelist for the URL value. */
  sortOptions: readonly SortOption[];
  /** Sort applied when the URL says nothing. Kept out of the URL as well. */
  defaultSortBy: TSortBy;
  /** Direction applied when the URL says nothing. */
  defaultSortOrder: SortDirection;
}

export interface AuthorDateFilter<TSortBy extends string> {
  filterState: ComputedRef<AuthorDateFilterState<TSortBy>>;
  searchParams: ComputedRef<AuthorDateSearchParams>;
  hasActiveFilters: ComputedRef<boolean>;

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

/** The state of an empty address: no filter, and the list's default sort. */
function createDefaultState<TSortBy extends string>(
  config: AuthorDateFilterConfig<TSortBy>,
): AuthorDateFilterState<TSortBy> {
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
function reducer<TSortBy extends string>(
  config: AuthorDateFilterConfig<TSortBy>,
  state: AuthorDateFilterState<TSortBy>,
  action: FilterAction<TSortBy>,
): AuthorDateFilterState<TSortBy> {
  const newState: AuthorDateFilterState<TSortBy> = {
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

    default:
      return reduceSortOrClear(newState, action, config.sortOptions, () =>
        createDefaultState(config),
      );
  }
}

function parseQueryToState<TSortBy extends string>(
  config: AuthorDateFilterConfig<TSortBy>,
  query: LocationQuery,
): AuthorDateFilterState<TSortBy> {
  const state = createDefaultState(config);

  state.search = parseStringFromUrl(
    toQueryValue(query.search),
    MAX_SEARCH_LENGTH,
  );

  if (query.authors) {
    for (const author of String(query.authors).split(",")) {
      const trimmed = author.trim().slice(0, MAX_AUTHOR_LENGTH);
      if (trimmed) state.authors.add(trimmed);
    }
  }

  state.createdFromUtc = parseDateFromUrl(toQueryValue(query.createdFromUtc));
  state.createdToUtc = parseDateFromUrl(toQueryValue(query.createdToUtc));
  state.sortBy = validateSortField(
    toQueryValue(query.sortBy),
    config.sortOptions.map((o) => o.value),
    config.defaultSortBy,
  ) as TSortBy;
  state.sortOrder = parseSortDirection(
    toQueryValue(query.sortOrder),
    config.defaultSortOrder,
  );

  return state;
}

function buildQueryFromState<TSortBy extends string>(
  config: AuthorDateFilterConfig<TSortBy>,
  state: AuthorDateFilterState<TSortBy>,
): Record<string, string> {
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

/** The request of the list for a state, a page and a page size. */
function authorDateSearchParams(
  state: AuthorDateFilterState,
  pageNumber: number | undefined,
  size: number,
): AuthorDateSearchParams {
  const params: AuthorDateSearchParams = {};

  if (state.search) params.search = state.search;
  if (state.authors.size > 0) params.authors = [...state.authors];

  if (state.createdFromUtc)
    params.createdFromUtc = dateToApiStart(state.createdFromUtc);
  if (state.createdToUtc)
    params.createdToUtc = dateToApiEnd(state.createdToUtc);

  params.sortBy = state.sortBy;
  params.sortOrder = state.sortOrder;

  if (pageNumber) params.number = pageNumber;

  params.size = size;
  return params;
}

/** Sort is left out: a view preference, not a filter that reduces results. */
function hasActiveAuthorDateFilters(state: AuthorDateFilterState): boolean {
  return (
    state.search !== "" ||
    state.authors.size > 0 ||
    state.createdFromUtc !== null ||
    state.createdToUtc !== null
  );
}

/**
 * The composable for one list. The address is the one source of the state;
 * call at module level, so the dispatcher that batches actions into one
 * navigation outlives the component.
 */
export function createAuthorDateFilter<TSortBy extends string = string>(
  config: AuthorDateFilterConfig<TSortBy>,
): () => AuthorDateFilter<TSortBy> {
  type State = AuthorDateFilterState<TSortBy>;
  type Action = FilterAction<TSortBy>;

  const reduce = (state: State, action: Action) =>
    reducer(config, state, action);

  const dispatcher = createFilterDispatcher<State, Action>({
    buildQuery: (state) => buildQueryFromState(config, state),
    name: config.name,
  });

  return function useAuthorDateFilter(): AuthorDateFilter<TSortBy> {
    const route = useRoute();
    const router = useRouter();
    const pageSize = usePaging().pageSize;

    dispatcher.setRouter(router);

    const filterState = computed<State>(() =>
      parseQueryToState(config, route.query),
    );

    const dispatch = (action: Action) =>
      dispatcher.dispatch(action, reduce, () => filterState.value);

    const searchParams = computed(() =>
      authorDateSearchParams(
        filterState.value,
        parsePageNumber(route.query.number),
        pageSize.value,
      ),
    );

    const hasActiveFilters = computed(() =>
      hasActiveAuthorDateFilters(filterState.value),
    );

    const setSearch = (search: string) =>
      dispatch({ type: "SET_SEARCH", search });
    const addAuthor = (username: string) =>
      dispatch({ type: "ADD_AUTHOR", username });
    const removeAuthor = (username: string) =>
      dispatch({ type: "REMOVE_AUTHOR", username });
    const clearAuthors = () => dispatch({ type: "CLEAR_AUTHORS" });
    const setDateRange = (from: string | null, to: string | null) =>
      dispatch({ type: "SET_DATE_RANGE", from, to });
    const { setSort, toggleSortOrder, clearFilters } =
      sortOrClearDispatchers(dispatch);

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
