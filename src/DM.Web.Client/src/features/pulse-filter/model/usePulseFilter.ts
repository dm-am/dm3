import { computed, type ComputedRef } from "vue";
import { useRoute, useRouter } from "vue-router";
import type { LocationQuery } from "vue-router";
import { usePaging } from "@/shared/lib/composables/usePaging";
import { createFilterDispatcher } from "@/shared/lib/composables/createFilterDispatcher";
import type { PulseSearchParams } from "@/entities/game";

// =============================================================================
// TYPES
// =============================================================================

export type PulseSortBy = "lastreview" | "rating" | "reviewcount";

export interface PulseFilterState {
  search: string;
  sortBy: PulseSortBy;
  sortOrder: "asc" | "desc";
  minRating: number | null;
  maxRating: number | null;
  authorUsernames: Set<string>;
  createdFrom: string | null;
  createdTo: string | null;
  gameId: string | null;
}

export interface PulseFilterComposable {
  filterState: ComputedRef<PulseFilterState>;
  searchParams: ComputedRef<PulseSearchParams>;
  hasActiveFilters: ComputedRef<boolean>;

  setSearch: (search: string) => void;
  setSort: (sortBy: PulseSortBy, sortOrder?: "asc" | "desc") => void;
  setRatingRange: (min: number | null, max: number | null) => void;
  addAuthor: (username: string) => void;
  removeAuthor: (username: string) => void;
  setCreatedRange: (from: string | null, to: string | null) => void;
  setGameId: (gameId: string | null) => void;
  clearFilters: () => void;
}

// =============================================================================
// ACTION TYPES
// =============================================================================

type PulseFilterAction =
  | { type: "SET_SEARCH"; search: string }
  | { type: "SET_SORT"; sortBy: PulseSortBy; sortOrder?: "asc" | "desc" }
  | { type: "SET_RATING_RANGE"; min: number | null; max: number | null }
  | { type: "ADD_AUTHOR"; username: string }
  | { type: "REMOVE_AUTHOR"; username: string }
  | { type: "SET_CREATED_RANGE"; from: string | null; to: string | null }
  | { type: "SET_GAME_ID"; gameId: string | null }
  | { type: "CLEAR_FILTERS" };

// =============================================================================
// PURE REDUCER
// =============================================================================

function createDefaultState(): PulseFilterState {
  return {
    search: "",
    sortBy: "lastreview",
    sortOrder: "desc",
    minRating: null,
    maxRating: null,
    authorUsernames: new Set(),
    createdFrom: null,
    createdTo: null,
    gameId: null,
  };
}

function reducer(
  state: PulseFilterState,
  action: PulseFilterAction,
): PulseFilterState {
  switch (action.type) {
    case "SET_SEARCH": {
      if (action.search === state.search) return state;
      return { ...state, search: action.search };
    }

    case "SET_SORT": {
      const sortOrder = action.sortOrder ?? "desc";
      if (action.sortBy === state.sortBy && sortOrder === state.sortOrder)
        return state;
      return { ...state, sortBy: action.sortBy, sortOrder };
    }

    case "SET_RATING_RANGE": {
      if (action.min === state.minRating && action.max === state.maxRating)
        return state;
      return { ...state, minRating: action.min, maxRating: action.max };
    }

    case "ADD_AUTHOR": {
      if (state.authorUsernames.has(action.username)) return state;
      const authorUsernames = new Set(state.authorUsernames);
      authorUsernames.add(action.username);
      return { ...state, authorUsernames };
    }

    case "REMOVE_AUTHOR": {
      if (!state.authorUsernames.has(action.username)) return state;
      const authorUsernames = new Set(state.authorUsernames);
      authorUsernames.delete(action.username);
      return { ...state, authorUsernames };
    }

    case "SET_CREATED_RANGE": {
      if (action.from === state.createdFrom && action.to === state.createdTo)
        return state;
      return { ...state, createdFrom: action.from, createdTo: action.to };
    }

    case "SET_GAME_ID": {
      if (action.gameId === state.gameId) return state;
      return { ...state, gameId: action.gameId };
    }

    case "CLEAR_FILTERS": {
      const defaults = createDefaultState();
      const isAlreadyDefault =
        state.search === defaults.search &&
        state.sortBy === defaults.sortBy &&
        state.sortOrder === defaults.sortOrder &&
        state.minRating === defaults.minRating &&
        state.maxRating === defaults.maxRating &&
        state.authorUsernames.size === 0 &&
        state.createdFrom === defaults.createdFrom &&
        state.createdTo === defaults.createdTo &&
        state.gameId === defaults.gameId;
      return isAlreadyDefault ? state : defaults;
    }

    default: {
      const _exhaustive: never = action;
      return _exhaustive;
    }
  }
}

// =============================================================================
// PARSING & BUILDING
// =============================================================================

const validSortByValues = new Set<string>([
  "lastreview",
  "rating",
  "reviewcount",
]);

// Strict "YYYY-MM-DD" — invalid date params from a hand-edited URL are
// ignored instead of producing an Invalid Date downstream
const dateFormatRegex = /^\d{4}-\d{2}-\d{2}$/;

function isValidDateParam(value: string): boolean {
  return dateFormatRegex.test(value) && !isNaN(Date.parse(value));
}

function parseQueryToState(query: LocationQuery): PulseFilterState {
  const state = createDefaultState();

  if (query.q) {
    state.search = String(query.q).slice(0, 200);
  }

  if (query.sort) {
    const sortBy = String(query.sort);
    if (validSortByValues.has(sortBy)) {
      state.sortBy = sortBy as PulseSortBy;
    }
  }

  if (query.order) {
    const order = String(query.order);
    if (order === "asc" || order === "desc") {
      state.sortOrder = order;
    }
  }

  if (query.minRating) {
    const rating = parseInt(String(query.minRating), 10);
    if (!isNaN(rating)) {
      state.minRating = rating;
    }
  }

  if (query.maxRating) {
    const rating = parseInt(String(query.maxRating), 10);
    if (!isNaN(rating)) {
      state.maxRating = rating;
    }
  }

  if (query.authors) {
    const authors = String(query.authors)
      .split(",")
      .map((s) => s.trim())
      .filter(Boolean);
    state.authorUsernames = new Set(authors);
  }

  if (query.createdFrom) {
    const createdFrom = String(query.createdFrom);
    if (isValidDateParam(createdFrom)) {
      state.createdFrom = createdFrom;
    }
  }

  if (query.createdTo) {
    const createdTo = String(query.createdTo);
    if (isValidDateParam(createdTo)) {
      state.createdTo = createdTo;
    }
  }

  if (query.game) {
    state.gameId = String(query.game);
  }

  return state;
}

function buildQueryFromState(state: PulseFilterState): Record<string, string> {
  const query: Record<string, string> = {};

  if (state.search) query.q = state.search;
  if (state.sortBy !== "lastreview") query.sort = state.sortBy;
  if (state.sortOrder !== "desc") query.order = state.sortOrder;
  if (state.minRating !== null) query.minRating = String(state.minRating);
  if (state.maxRating !== null) query.maxRating = String(state.maxRating);
  if (state.authorUsernames.size > 0)
    query.authors = [...state.authorUsernames].join(",");
  if (state.createdFrom) query.createdFrom = state.createdFrom;
  if (state.createdTo) query.createdTo = state.createdTo;
  if (state.gameId) query.game = state.gameId;

  return query;
}

// =============================================================================
// MODULE-LEVEL DISPATCHER
// =============================================================================

const dispatcher = createFilterDispatcher<PulseFilterState, PulseFilterAction>({
  buildQuery: buildQueryFromState,
  name: "usePulseFilter",
});

// =============================================================================
// COMPOSABLE
// =============================================================================

export function usePulseFilter(): PulseFilterComposable {
  const route = useRoute();
  const router = useRouter();
  const { entitiesPerPage } = usePaging();

  dispatcher.setRouter(router);

  const filterState = computed<PulseFilterState>(() =>
    parseQueryToState(route.query),
  );
  const getCurrentState = () => filterState.value;
  const dispatch = (action: PulseFilterAction) =>
    dispatcher.dispatch(action, reducer, getCurrentState);

  const searchParams = computed<PulseSearchParams>(() => {
    const state = filterState.value;
    const params: PulseSearchParams = {};

    if (state.search) params.search = state.search;
    params.sortBy = state.sortBy;
    params.sortOrder = state.sortOrder;
    if (state.minRating !== null) params.minRating = state.minRating;
    if (state.maxRating !== null) params.maxRating = state.maxRating;
    if (state.authorUsernames.size > 0)
      params.authorUsernames = [...state.authorUsernames];
    if (state.createdFrom) params.createdFrom = state.createdFrom;
    if (state.createdTo) params.createdTo = state.createdTo;
    if (state.gameId) params.gameId = state.gameId;

    const numberParam = route.query.number;
    if (numberParam) {
      const num = parseInt(String(numberParam), 10);
      if (!isNaN(num) && num > 0) params.number = num;
    }

    params.size = entitiesPerPage.value;
    return params;
  });

  // Sorting is intentionally excluded: it changes the order, not the
  // subset of data (same as games/blogs/topics filter composables)
  const hasActiveFilters = computed(() => {
    const state = filterState.value;
    return (
      state.search !== "" ||
      state.minRating !== null ||
      state.maxRating !== null ||
      state.authorUsernames.size > 0 ||
      state.createdFrom !== null ||
      state.createdTo !== null ||
      state.gameId !== null
    );
  });

  const setSearch = (search: string) =>
    dispatch({ type: "SET_SEARCH", search });
  const setSort = (sortBy: PulseSortBy, sortOrder?: "asc" | "desc") =>
    dispatch({ type: "SET_SORT", sortBy, sortOrder });
  const setRatingRange = (min: number | null, max: number | null) =>
    dispatch({ type: "SET_RATING_RANGE", min, max });
  const addAuthor = (username: string) =>
    dispatch({ type: "ADD_AUTHOR", username });
  const removeAuthor = (username: string) =>
    dispatch({ type: "REMOVE_AUTHOR", username });
  const setCreatedRange = (from: string | null, to: string | null) =>
    dispatch({ type: "SET_CREATED_RANGE", from, to });
  const setGameId = (gameId: string | null) =>
    dispatch({ type: "SET_GAME_ID", gameId });
  const clearFilters = () => dispatch({ type: "CLEAR_FILTERS" });

  return {
    filterState,
    searchParams,
    hasActiveFilters,
    setSearch,
    setSort,
    setRatingRange,
    addAuthor,
    removeAuthor,
    setCreatedRange,
    setGameId,
    clearFilters,
  };
}
