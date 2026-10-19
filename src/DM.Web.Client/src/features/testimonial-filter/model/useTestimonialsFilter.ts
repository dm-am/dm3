import { computed, type ComputedRef } from "vue";
import { useRoute, useRouter } from "vue-router";
import type { LocationQuery } from "vue-router";
import { createFilterDispatcher } from "@/shared/lib/composables/createFilterDispatcher";
import {
  applySortAction,
  parseSortDirection,
  validateSortField,
  parsePageNumber,
} from "@/shared/lib/filters";
import type {
  TestimonialsFilterState,
  TestimonialsSearchParams,
  TestimonialSortBy,
} from "./types";
import { DEFAULT_FILTER_STATE, SORT_OPTIONS } from "./types";

export interface TestimonialsFilterComposable {
  filterState: ComputedRef<TestimonialsFilterState>;
  searchParams: ComputedRef<TestimonialsSearchParams>;
  hasActiveFilters: ComputedRef<boolean>;

  setSearch: (search: string) => void;
  setSort: (sortBy: TestimonialSortBy, sortOrder?: "asc" | "desc") => void;
  toggleSortOrder: () => void;
  clearFilters: () => void;
}

type TestimonialsFilterAction =
  | { type: "SET_SEARCH"; search: string }
  | { type: "SET_SORT"; sortBy: TestimonialSortBy; sortOrder?: "asc" | "desc" }
  | { type: "TOGGLE_SORT_ORDER" }
  | { type: "CLEAR_FILTERS" };

function reducer(
  state: TestimonialsFilterState,
  action: TestimonialsFilterAction,
): TestimonialsFilterState {
  const newState: TestimonialsFilterState = { ...state };

  switch (action.type) {
    case "SET_SEARCH":
      newState.search = action.search;
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

const dispatcher = createFilterDispatcher<
  TestimonialsFilterState,
  TestimonialsFilterAction
>({
  buildQuery: buildQueryFromState,
  name: "useTestimonialsFilter",
});

// The URL is the single source of truth; actions batch through one module-level debounce.
export function useTestimonialsFilter(): TestimonialsFilterComposable {
  const route = useRoute();
  const router = useRouter();

  dispatcher.setRouter(router);

  const filterState = computed<TestimonialsFilterState>(() =>
    parseQueryToState(route.query),
  );

  const getCurrentState = () => filterState.value;

  const dispatch = (action: TestimonialsFilterAction) =>
    dispatcher.dispatch(action, reducer, getCurrentState);

  const searchParams = computed<TestimonialsSearchParams>(() => {
    const state = filterState.value;
    const params: TestimonialsSearchParams = {};

    if (state.search) params.search = state.search;
    params.sortBy = state.sortBy;
    params.sortOrder = state.sortOrder;

    const pageNumber = parsePageNumber(route.query.number);
    if (pageNumber) params.number = pageNumber;

    params.size = 10; // Testimonials use fixed page size
    return params;
  });

  const hasActiveFilters = computed(() => {
    const state = filterState.value;
    const def = DEFAULT_FILTER_STATE;
    return state.search !== def.search;
  });

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
