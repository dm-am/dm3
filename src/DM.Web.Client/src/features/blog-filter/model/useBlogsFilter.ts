import { computed } from "vue";
import { useRoute, useRouter } from "vue-router";
import type { LocationQuery } from "vue-router";
import { usePaging } from "@/shared/lib/composables/usePaging";
import { createFilterDispatcher } from "@/shared/lib/composables/createFilterDispatcher";
import {
  reduceSortOrClear,
  sortOrClearDispatchers,
  parseDateFromUrl,
  dateToApiStart,
  dateToApiEnd,
  toQueryValue,
  parsePageNumber,
} from "@/shared/lib/filters";
import type {
  BlogsFilterState,
  BlogsSearchParams,
  StatusFilter,
} from "./types";
import { SORT_OPTIONS, STATUS_OPTIONS, DEFAULT_FILTER_STATE } from "./types";
import type { BlogStatus } from "@/entities/blog";
import type { SortDirection } from "@/shared/lib/filters/types";

type BlogsFilterAction =
  | { type: "SET_SEARCH"; search: string }
  | { type: "SET_STATUS"; status: StatusFilter }
  | { type: "ADD_HOST"; username: string }
  | { type: "REMOVE_HOST"; username: string }
  | { type: "SET_CREATED_RANGE"; fromUtc: string | null; toUtc: string | null }
  | {
      type: "SET_ACTIVATED_RANGE";
      fromUtc: string | null;
      toUtc: string | null;
    }
  | { type: "SET_CLOSED_RANGE"; fromUtc: string | null; toUtc: string | null }
  | { type: "SET_SORT"; sortBy: string; sortOrder?: SortDirection }
  | { type: "TOGGLE_SORT_ORDER" }
  | { type: "CLEAR_FILTERS" };

function reducer(
  state: BlogsFilterState,
  action: BlogsFilterAction,
): BlogsFilterState {
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

    default:
      return reduceSortOrClear(
        newState,
        action,
        SORT_OPTIONS,
        createDefaultState,
      );
  }
}

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

  state.createdFromUtc = parseDateFromUrl(toQueryValue(query.createdFromUtc));
  state.createdToUtc = parseDateFromUrl(toQueryValue(query.createdToUtc));
  state.activatedFromUtc = parseDateFromUrl(
    toQueryValue(query.activatedFromUtc),
  );
  state.activatedToUtc = parseDateFromUrl(toQueryValue(query.activatedToUtc));
  state.closedFromUtc = parseDateFromUrl(toQueryValue(query.closedFromUtc));
  state.closedToUtc = parseDateFromUrl(toQueryValue(query.closedToUtc));

  const sortByRaw = query.sortBy as string;
  if (validSortByValues.has(sortByRaw)) state.sortBy = sortByRaw;

  const sortOrderRaw = query.sortOrder as string;
  if (sortOrderRaw === "asc" || sortOrderRaw === "desc")
    state.sortOrder = sortOrderRaw;

  return state;
}

function buildQueryFromState(state: BlogsFilterState): Record<string, string> {
  const query: Record<string, string> = {};

  if (state.search) query.search = state.search;
  if (state.status !== "any") query.status = state.status;
  if (state.hostUsernames.size > 0)
    query.hosts = [...state.hostUsernames].join(",");
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

const dispatcher = createFilterDispatcher<BlogsFilterState, BlogsFilterAction>({
  buildQuery: buildQueryFromState,
  name: "useBlogsFilter",
});

// The URL is the single source of truth; actions batch through one module-level debounce.
export function useBlogsFilter() {
  const route = useRoute();
  const router = useRouter();
  const { pageSize } = usePaging();

  dispatcher.setRouter(router);

  const filterState = computed<BlogsFilterState>(() =>
    parseQueryToState(route.query),
  );

  const getCurrentState = () => filterState.value;

  const dispatch = (action: BlogsFilterAction) =>
    dispatcher.dispatch(action, reducer, getCurrentState);

  const searchParams = computed<BlogsSearchParams>(() => {
    const state = filterState.value;
    const params: BlogsSearchParams = {};

    if (state.search) params.search = state.search;
    if (state.status !== "any") params.status = state.status as BlogStatus;
    if (state.hostUsernames.size > 0)
      params.hostUsernames = [...state.hostUsernames];

    if (state.createdFromUtc)
      params.createdFromUtc = dateToApiStart(state.createdFromUtc);
    if (state.createdToUtc)
      params.createdToUtc = dateToApiEnd(state.createdToUtc);
    if (state.activatedFromUtc)
      params.activatedFromUtc = dateToApiStart(state.activatedFromUtc);
    if (state.activatedToUtc)
      params.activatedToUtc = dateToApiEnd(state.activatedToUtc);
    if (state.closedFromUtc)
      params.closedFromUtc = dateToApiStart(state.closedFromUtc);
    if (state.closedToUtc) params.closedToUtc = dateToApiEnd(state.closedToUtc);

    params.sortBy = state.sortBy;
    params.sortOrder = state.sortOrder;

    const pageNumber = parsePageNumber(route.query.number);
    if (pageNumber) params.number = pageNumber;

    params.size = pageSize.value;
    return params;
  });

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

  const setSearch = (search: string) =>
    dispatch({ type: "SET_SEARCH", search });
  const setStatus = (status: StatusFilter) =>
    dispatch({ type: "SET_STATUS", status });
  const addHost = (username: string) =>
    dispatch({ type: "ADD_HOST", username });
  const removeHost = (username: string) =>
    dispatch({ type: "REMOVE_HOST", username });
  const setCreatedRange = (fromUtc: string | null, toUtc: string | null) =>
    dispatch({ type: "SET_CREATED_RANGE", fromUtc, toUtc });
  const setActivatedRange = (fromUtc: string | null, toUtc: string | null) =>
    dispatch({ type: "SET_ACTIVATED_RANGE", fromUtc, toUtc });
  const setClosedRange = (fromUtc: string | null, toUtc: string | null) =>
    dispatch({ type: "SET_CLOSED_RANGE", fromUtc, toUtc });
  const { setSort, toggleSortOrder, clearFilters } =
    sortOrClearDispatchers(dispatch);

  return {
    filterState,
    searchParams,
    hasActiveFilters,
    setSearch,
    setStatus,
    addHost,
    removeHost,
    setCreatedRange,
    setActivatedRange,
    setClosedRange,
    setSort,
    toggleSortOrder,
    clearFilters,
  };
}
