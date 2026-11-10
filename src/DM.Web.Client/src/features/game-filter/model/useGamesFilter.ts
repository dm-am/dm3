import { computed } from "vue";
import { useRoute, useRouter } from "vue-router";
import { usePaging } from "@/shared/lib/composables/usePaging";
import { createFilterDispatcher } from "@/shared/lib/composables/createFilterDispatcher";
import {
  dateToApiStart,
  dateToApiEnd,
  parsePageNumber,
} from "@/shared/lib/filters";
import type {
  ClosedReasonFilter,
  GamesFilterState,
  GamesSearchParams,
  RecruitmentFilter,
  StatusValue,
} from "./types";
import { reducer, type GamesFilterAction } from "./gamesFilterReducer";
import { parseQueryToState, buildQueryFromState } from "./gamesFilterQuery";
import {
  GAME_DATE_RANGES,
  hasRange,
  type GameDateRangeKey,
} from "./gameDateRanges";
import type { SortDirection } from "@/shared/lib/filters/types";

const dispatcher = createFilterDispatcher<GamesFilterState, GamesFilterAction>({
  buildQuery: buildQueryFromState,
  name: "useGamesFilter",
});

function gamesSearchParams(
  state: GamesFilterState,
  pageNumber: number | undefined,
  size: number,
): GamesSearchParams {
  const params: GamesSearchParams = {};

  if (state.search) params.search = state.search;

  if (state.status) {
    params.status = state.status;
    if (state.status === "Active" && state.recruitmentFilter !== "any") {
      params.recruitmentFilter = state.recruitmentFilter;
    }
    if (state.status === "Closed" && state.closedReasonFilter !== "any") {
      params.closedReasonFilter = state.closedReasonFilter;
    }
  }

  if (state.requiredTags.size > 0)
    params.requiredTags = [...state.requiredTags];
  if (state.excludedTags.size > 0)
    params.excludedTags = [...state.excludedTags];
  if (state.hostUsernames.size > 0)
    params.hostUsernames = [...state.hostUsernames];

  // Whole days: from the start of the first one to the end of the last one
  for (const { from, to } of GAME_DATE_RANGES) {
    const fromDay = state[from];
    const toDay = state[to];
    if (fromDay) params[from] = dateToApiStart(fromDay);
    if (toDay) params[to] = dateToApiEnd(toDay);
  }

  params.sortBy = state.sortBy;
  params.sortOrder = state.sortOrder;

  if (pageNumber) params.number = pageNumber;

  params.size = size;
  return params;
}

// A sort alone does not filter.
function isFiltered(state: GamesFilterState): boolean {
  return (
    state.search !== "" ||
    state.status !== null ||
    state.requiredTags.size > 0 ||
    state.excludedTags.size > 0 ||
    state.hostUsernames.size > 0 ||
    GAME_DATE_RANGES.some((range) => hasRange(state, range))
  );
}

// The URL is the single source of truth; actions batch through one module-level debounce.
export function useGamesFilter() {
  const route = useRoute();
  const router = useRouter();
  const { pageSize } = usePaging();

  dispatcher.setRouter(router);

  const filterState = computed<GamesFilterState>(() =>
    parseQueryToState(route.query),
  );

  const dispatch = (action: GamesFilterAction) =>
    dispatcher.dispatch(action, reducer, () => filterState.value);

  const setRange =
    (range: GameDateRangeKey) =>
    (fromUtc: string | null, toUtc: string | null) =>
      dispatch({ type: "SET_DATE_RANGE", range, fromUtc, toUtc });

  return {
    filterState,
    searchParams: computed(() =>
      gamesSearchParams(
        filterState.value,
        parsePageNumber(route.query.number),
        pageSize.value,
      ),
    ),
    hasActiveFilters: computed(() => isFiltered(filterState.value)),

    setSearch: (search: string) => dispatch({ type: "SET_SEARCH", search }),
    setStatus: (status: StatusValue | null) =>
      dispatch({ type: "SET_STATUS", status }),
    clearStatus: () => dispatch({ type: "SET_STATUS", status: null }),
    setRecruitmentFilter: (value: RecruitmentFilter) =>
      dispatch({ type: "SET_RECRUITMENT_FILTER", value }),
    setClosedReasonFilter: (value: ClosedReasonFilter) =>
      dispatch({ type: "SET_CLOSED_REASON_FILTER", value }),
    addRequiredTag: (tagId: number) =>
      dispatch({ type: "ADD_REQUIRED_TAG", tagId }),
    addExcludedTag: (tagId: number) =>
      dispatch({ type: "ADD_EXCLUDED_TAG", tagId }),
    removeTag: (tagId: number) => dispatch({ type: "REMOVE_TAG", tagId }),
    addHost: (username: string) => dispatch({ type: "ADD_HOST", username }),
    removeHost: (username: string) =>
      dispatch({ type: "REMOVE_HOST", username }),
    setCreatedRange: setRange("created"),
    setActivatedRange: setRange("activated"),
    setClosedRange: setRange("closed"),
    setRecruitmentStartedRange: setRange("recruitmentstarted"),
    setSort: (sortBy: string, sortOrder?: SortDirection) =>
      dispatch({ type: "SET_SORT", sortBy, sortOrder }),
    toggleSortOrder: () => dispatch({ type: "TOGGLE_SORT_ORDER" }),
    clearFilters: () => dispatch({ type: "CLEAR_FILTERS" }),
    validateTagFilters: (validTagIds: Set<number>) =>
      dispatch({ type: "VALIDATE_TAGS", validTagIds }),
  };
}
