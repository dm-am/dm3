import { computed, type ComputedRef } from "vue";
import { useRoute, useRouter } from "vue-router";
import type { LocationQuery } from "vue-router";
import { usePaging, createFilterDispatcher } from "@/shared/lib/composables";
import type { PulseSearchParams } from "@/entities/game";

// =============================================================================
// TYPES
// =============================================================================

export type PulseSortBy = "lastreview" | "rating";
export type MinRatingFilter = null | 1 | 3;

export interface PulseFilterState {
  search: string;
  sortBy: PulseSortBy;
  sortOrder: "asc" | "desc";
  minRating: MinRatingFilter;
  gameId: string | null;
}

export interface PulseFilterComposable {
  filterState: ComputedRef<PulseFilterState>;
  searchParams: ComputedRef<PulseSearchParams>;
  hasActiveFilters: ComputedRef<boolean>;

  setSearch: (search: string) => void;
  setSort: (sortBy: PulseSortBy, sortOrder?: "asc" | "desc") => void;
  setMinRating: (minRating: MinRatingFilter) => void;
  setGameId: (gameId: string | null) => void;
  clearFilters: () => void;
}

// =============================================================================
// ACTION TYPES
// =============================================================================

type PulseFilterAction =
  | { type: "SET_SEARCH"; search: string }
  | { type: "SET_SORT"; sortBy: PulseSortBy; sortOrder?: "asc" | "desc" }
  | { type: "SET_MIN_RATING"; minRating: MinRatingFilter }
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
    gameId: null,
  };
}

function reducer(state: PulseFilterState, action: PulseFilterAction): PulseFilterState {
  const newState = { ...state };

  switch (action.type) {
    case "SET_SEARCH":
      newState.search = action.search;
      return newState;

    case "SET_SORT":
      newState.sortBy = action.sortBy;
      if (action.sortOrder) {
        newState.sortOrder = action.sortOrder;
      } else {
        // Default: lastreview=desc, rating=desc
        newState.sortOrder = "desc";
      }
      return newState;

    case "SET_MIN_RATING":
      newState.minRating = action.minRating;
      return newState;

    case "SET_GAME_ID":
      newState.gameId = action.gameId;
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
// PARSING & BUILDING
// =============================================================================

const validSortByValues = new Set<string>(["lastreview", "rating"]);

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
    if (rating === 1 || rating === 3) {
      state.minRating = rating;
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

  const filterState = computed<PulseFilterState>(() => parseQueryToState(route.query));
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
    if (state.gameId) params.gameId = state.gameId;

    const numberParam = route.query.number;
    if (numberParam) {
      const num = parseInt(String(numberParam), 10);
      if (!isNaN(num) && num > 0) params.number = num;
    }

    params.size = entitiesPerPage.value;
    return params;
  });

  const hasActiveFilters = computed(() => {
    const state = filterState.value;
    return (
      state.search !== "" ||
      state.minRating !== null ||
      state.gameId !== null
    );
  });

  const setSearch = (search: string) => dispatch({ type: "SET_SEARCH", search });
  const setSort = (sortBy: PulseSortBy, sortOrder?: "asc" | "desc") =>
    dispatch({ type: "SET_SORT", sortBy, sortOrder });
  const setMinRating = (minRating: MinRatingFilter) =>
    dispatch({ type: "SET_MIN_RATING", minRating });
  const setGameId = (gameId: string | null) =>
    dispatch({ type: "SET_GAME_ID", gameId });
  const clearFilters = () => dispatch({ type: "CLEAR_FILTERS" });

  return {
    filterState,
    searchParams,
    hasActiveFilters,
    setSearch,
    setSort,
    setMinRating,
    setGameId,
    clearFilters,
  };
}
