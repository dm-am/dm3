import { computed, type ComputedRef } from "vue";
import { useRoute, useRouter } from "vue-router";
import type { LocationQuery } from "vue-router";
import { usePaging } from "@/shared/lib/composables/usePaging";
import { createFilterDispatcher } from "@/shared/lib/composables/createFilterDispatcher";
import {
  applySortAction,
  dateToApiEnd,
  dateToApiStart,
  parseDateFromUrl,
  parseSortDirection,
  validateSortField,
  parsePageNumber,
  toQueryValue,
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

export interface PollsFilterComposable {
  filterState: ComputedRef<PollsFilterState>;
  searchParams: ComputedRef<PollsSearchParams>;
  hasActiveFilters: ComputedRef<boolean>;

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

const validSortByValues = SORT_OPTIONS.map((o) => o.value);
const validStatusValues = new Set<string>(STATUS_OPTIONS.map((o) => o.value));
const validPollTypeValues = new Set<string>(
  POLL_TYPE_OPTIONS.map((o) => o.value),
);

function createDefaultState(): PollsFilterState {
  return { ...DEFAULT_FILTER_STATE };
}

function parseQueryToState(query: LocationQuery): PollsFilterState {
  const state = createDefaultState();

  if (query.status) {
    const statusStr = String(query.status);
    if (validStatusValues.has(statusStr)) {
      state.status = statusStr as PollStatus | "";
    }
  }

  if (query.pollType) {
    const pollTypeStr = String(query.pollType);
    if (validPollTypeValues.has(pollTypeStr)) {
      state.pollType = pollTypeStr as "anonymous" | "public" | "";
    }
  }

  if (query.search) {
    state.search = String(query.search).slice(0, 200);
  }

  // A day of the address, YYYY-MM-DD; an ISO moment is read as its day, anything else as none.
  const day = (value: LocationQuery[string]) =>
    parseDateFromUrl(toQueryValue(value)) ?? "";
  state.startsFromUtc = day(query.startsFromUtc);
  state.startsToUtc = day(query.startsToUtc);
  state.endsFromUtc = day(query.endsFromUtc);
  state.endsToUtc = day(query.endsToUtc);

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

const dispatcher = createFilterDispatcher<PollsFilterState, PollsFilterAction>({
  buildQuery: buildQueryFromState,
  name: "usePollsFilter",
});

// The URL is the single source of truth; actions batch through one module-level debounce.
export function usePollsFilter(): PollsFilterComposable {
  const route = useRoute();
  const router = useRouter();
  const { pageSize } = usePaging();

  dispatcher.setRouter(router);

  const filterState = computed<PollsFilterState>(() =>
    parseQueryToState(route.query),
  );

  const getCurrentState = () => filterState.value;

  const dispatch = (action: PollsFilterAction) =>
    dispatcher.dispatch(action, reducer, getCurrentState);

  // A day of the address is the whole day, as every other list asks for it.
  const searchParams = computed<PollsSearchParams>(() => {
    const state = filterState.value;
    const params: PollsSearchParams = {};

    if (state.status) params.status = state.status;
    if (state.pollType === "anonymous") params.isAnonymous = true;
    else if (state.pollType === "public") params.isAnonymous = false;
    if (state.search) params.search = state.search;
    if (state.startsFromUtc)
      params.startsFromUtc = dateToApiStart(state.startsFromUtc);
    if (state.startsToUtc) params.startsToUtc = dateToApiEnd(state.startsToUtc);
    if (state.endsFromUtc)
      params.endsFromUtc = dateToApiStart(state.endsFromUtc);
    if (state.endsToUtc) params.endsToUtc = dateToApiEnd(state.endsToUtc);
    params.sortBy = state.sortBy;
    params.sortOrder = state.sortOrder;

    const pageNumber = parsePageNumber(route.query.number);
    if (pageNumber) params.number = pageNumber;

    params.size = pageSize.value;
    return params;
  });

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
