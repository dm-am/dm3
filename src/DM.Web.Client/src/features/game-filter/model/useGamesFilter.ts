import { computed, type ComputedRef } from "vue";
import { useRoute, useRouter } from "vue-router";
import type { LocationQuery } from "vue-router";
import { usePaging } from "@/shared/lib/composables/usePaging";
import { createFilterDispatcher } from "@/shared/lib/composables/createFilterDispatcher";
import {
  parseDateFromUrl,
  dateToApiStart,
  dateToApiEnd,
  toQueryValue,
} from "@/shared/lib/filters";
import type {
  GamesFilterState,
  GamesSearchParams,
  RecruitmentFilter,
  ClosedReasonFilter,
  StatusValue,
} from "./types";
import {
  SORT_OPTIONS,
  STATUS_OPTIONS,
  RECRUITMENT_FILTER_OPTIONS,
  CLOSED_REASON_FILTER_OPTIONS,
} from "./types";

// =============================================================================
// RETURN TYPE
// =============================================================================

export interface GamesFilterComposable {
  /** Current filter state (computed from URL) */
  filterState: ComputedRef<GamesFilterState>;
  /** API search parameters (computed from filterState) */
  searchParams: ComputedRef<GamesSearchParams>;
  /** Active filters for UI chips */
  activeFilters: ComputedRef<
    { key: string; label: string; type: "status" | "tag" | "host" }[]
  >;
  /** Whether any filters are active */
  hasActiveFilters: ComputedRef<boolean>;

  // Actions
  setSearch: (search: string) => void;
  setStatus: (status: StatusValue | null) => void;
  clearStatus: () => void;
  setRecruitmentFilter: (value: RecruitmentFilter) => void;
  setClosedReasonFilter: (value: ClosedReasonFilter) => void;
  addRequiredTag: (tagId: number) => void;
  addExcludedTag: (tagId: number) => void;
  removeTag: (tagId: number) => void;
  addHost: (username: string) => void;
  removeHost: (username: string) => void;
  clearHosts: () => void;
  setCreatedRange: (fromUtc: string | null, toUtc: string | null) => void;
  setActivatedRange: (fromUtc: string | null, toUtc: string | null) => void;
  setClosedRange: (fromUtc: string | null, toUtc: string | null) => void;
  setRecruitmentStartedRange: (
    fromUtc: string | null,
    toUtc: string | null,
  ) => void;
  clearDateRanges: () => void;
  setSort: (sortBy: string, sortOrder?: "asc" | "desc") => void;
  toggleSortOrder: () => void;
  clearFilters: () => void;
  validateTagFilters: (validTagIds: Set<number>) => void;
}

// =============================================================================
// ACTION TYPES (Discriminated Union)
// =============================================================================

type GamesFilterAction =
  | { type: "SET_SEARCH"; search: string }
  | { type: "SET_STATUS"; status: StatusValue | null }
  | { type: "SET_RECRUITMENT_FILTER"; value: RecruitmentFilter }
  | { type: "SET_CLOSED_REASON_FILTER"; value: ClosedReasonFilter }
  | { type: "ADD_REQUIRED_TAG"; tagId: number }
  | { type: "ADD_EXCLUDED_TAG"; tagId: number }
  | { type: "REMOVE_TAG"; tagId: number }
  | { type: "ADD_HOST"; username: string }
  | { type: "REMOVE_HOST"; username: string }
  | { type: "CLEAR_HOSTS" }
  | { type: "SET_CREATED_RANGE"; fromUtc: string | null; toUtc: string | null }
  | {
      type: "SET_ACTIVATED_RANGE";
      fromUtc: string | null;
      toUtc: string | null;
    }
  | { type: "SET_CLOSED_RANGE"; fromUtc: string | null; toUtc: string | null }
  | {
      type: "SET_RECRUITMENT_STARTED_RANGE";
      fromUtc: string | null;
      toUtc: string | null;
    }
  | { type: "CLEAR_DATE_RANGES" }
  | { type: "SET_SORT"; sortBy: string; sortOrder?: "asc" | "desc" }
  | { type: "TOGGLE_SORT_ORDER" }
  | { type: "CLEAR_FILTERS" }
  | { type: "VALIDATE_TAGS"; validTagIds: Set<number> };

// =============================================================================
// PURE REDUCER (Testable, no side effects)
// =============================================================================

/**
 * Pure reducer function - applies action to state and returns new state.
 * This is the ONLY place where state transformation logic lives.
 */
function reducer(
  state: GamesFilterState,
  action: GamesFilterAction,
): GamesFilterState {
  // Clone state with new Set instances to avoid mutation
  const newState: GamesFilterState = {
    ...state,
    requiredTags: new Set(state.requiredTags),
    excludedTags: new Set(state.excludedTags),
    hostUsernames: new Set(state.hostUsernames),
  };

  switch (action.type) {
    case "SET_SEARCH":
      newState.search = action.search;
      return newState;

    case "SET_STATUS":
      // Reset sub-filters when status changes
      if (newState.status !== action.status) {
        newState.recruitmentFilter = "any";
        newState.closedReasonFilter = "any";
      }
      newState.status = action.status;
      return newState;

    case "SET_RECRUITMENT_FILTER":
      newState.recruitmentFilter = action.value;
      return newState;

    case "SET_CLOSED_REASON_FILTER":
      newState.closedReasonFilter = action.value;
      return newState;

    case "ADD_REQUIRED_TAG":
      newState.excludedTags.delete(action.tagId);
      newState.requiredTags.add(action.tagId);
      return newState;

    case "ADD_EXCLUDED_TAG":
      newState.requiredTags.delete(action.tagId);
      newState.excludedTags.add(action.tagId);
      return newState;

    case "REMOVE_TAG":
      newState.requiredTags.delete(action.tagId);
      newState.excludedTags.delete(action.tagId);
      return newState;

    case "ADD_HOST":
      newState.hostUsernames.add(action.username);
      return newState;

    case "REMOVE_HOST":
      newState.hostUsernames.delete(action.username);
      return newState;

    case "CLEAR_HOSTS":
      newState.hostUsernames.clear();
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

    case "SET_RECRUITMENT_STARTED_RANGE":
      newState.recruitmentStartedFromUtc = action.fromUtc;
      newState.recruitmentStartedToUtc = action.toUtc;
      return newState;

    case "CLEAR_DATE_RANGES":
      newState.createdFromUtc = null;
      newState.createdToUtc = null;
      newState.activatedFromUtc = null;
      newState.activatedToUtc = null;
      newState.closedFromUtc = null;
      newState.closedToUtc = null;
      newState.recruitmentStartedFromUtc = null;
      newState.recruitmentStartedToUtc = null;
      return newState;

    case "SET_SORT": {
      newState.sortBy = action.sortBy;
      if (action.sortOrder) {
        newState.sortOrder = action.sortOrder;
      } else {
        const option = SORT_OPTIONS.find((o) => o.value === action.sortBy);
        newState.sortOrder = option?.defaultDirection || "asc";
      }
      return newState;
    }

    case "TOGGLE_SORT_ORDER":
      newState.sortOrder = newState.sortOrder === "asc" ? "desc" : "asc";
      return newState;

    case "CLEAR_FILTERS":
      return createDefaultState();

    case "VALIDATE_TAGS": {
      let changed = false;
      for (const tagId of newState.requiredTags) {
        if (!action.validTagIds.has(tagId)) {
          newState.requiredTags.delete(tagId);
          changed = true;
        }
      }
      for (const tagId of newState.excludedTags) {
        if (!action.validTagIds.has(tagId)) {
          newState.excludedTags.delete(tagId);
          changed = true;
        }
      }
      // Return original state if nothing changed (optimization)
      return changed ? newState : state;
    }

    default: {
      // Exhaustiveness check - TypeScript will error if we miss a case
      const _exhaustive: never = action;
      return _exhaustive;
    }
  }
}

// =============================================================================
// VALIDATION & PARSING
// =============================================================================

const validSortByValues = new Set<string>(SORT_OPTIONS.map((o) => o.value));
const validStatusValues = new Set<string>(STATUS_OPTIONS.map((o) => o.value));
const validRecruitmentValues = new Set<string>(
  RECRUITMENT_FILTER_OPTIONS.map((o) => o.value),
);
const validClosedReasonValues = new Set<string>(
  CLOSED_REASON_FILTER_OPTIONS.map((o) => o.value),
);

function createDefaultState(): GamesFilterState {
  return {
    search: "",
    status: null,
    recruitmentFilter: "any",
    closedReasonFilter: "any",
    requiredTags: new Set(),
    excludedTags: new Set(),
    hostUsernames: new Set(),
    createdFromUtc: null,
    createdToUtc: null,
    activatedFromUtc: null,
    activatedToUtc: null,
    closedFromUtc: null,
    closedToUtc: null,
    recruitmentStartedFromUtc: null,
    recruitmentStartedToUtc: null,
    sortBy: "created",
    sortOrder: "desc",
  };
}

function parseQueryToState(query: LocationQuery): GamesFilterState {
  const state = createDefaultState();

  if (query.search) {
    state.search = String(query.search).slice(0, 200);
  }

  if (query.status) {
    const status = String(query.status);
    if (validStatusValues.has(status)) {
      state.status = status as StatusValue;
    }
  }

  if (query.recruitmentFilter) {
    const rf = String(query.recruitmentFilter);
    if (validRecruitmentValues.has(rf)) {
      state.recruitmentFilter = rf as RecruitmentFilter;
    }
  }

  if (query.closedReasonFilter) {
    const crf = String(query.closedReasonFilter);
    if (validClosedReasonValues.has(crf)) {
      state.closedReasonFilter = crf as ClosedReasonFilter;
    }
  }

  if (query.requiredTags) {
    for (const tag of String(query.requiredTags).split(",")) {
      const tagId = parseInt(tag, 10);
      if (!isNaN(tagId) && tagId > 0) state.requiredTags.add(tagId);
    }
  }

  if (query.excludedTags) {
    for (const tag of String(query.excludedTags).split(",")) {
      const tagId = parseInt(tag, 10);
      if (!isNaN(tagId) && tagId > 0) state.excludedTags.add(tagId);
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
  state.recruitmentStartedFromUtc = parseDateFromUrl(
    toQueryValue(query.recruitmentStartedFromUtc),
  );
  state.recruitmentStartedToUtc = parseDateFromUrl(
    toQueryValue(query.recruitmentStartedToUtc),
  );

  const sortByRaw = query.sortBy as string;
  if (validSortByValues.has(sortByRaw)) state.sortBy = sortByRaw;

  const sortOrderRaw = query.sortOrder as string;
  if (sortOrderRaw === "asc" || sortOrderRaw === "desc")
    state.sortOrder = sortOrderRaw;

  return state;
}

function buildQueryFromState(state: GamesFilterState): Record<string, string> {
  const query: Record<string, string> = {};

  if (state.search) query.search = state.search;
  if (state.status) query.status = state.status;
  if (state.status === "Active" && state.recruitmentFilter !== "any") {
    query.recruitmentFilter = state.recruitmentFilter;
  }
  if (state.status === "Closed" && state.closedReasonFilter !== "any") {
    query.closedReasonFilter = state.closedReasonFilter;
  }
  if (state.requiredTags.size > 0)
    query.requiredTags = [...state.requiredTags].join(",");
  if (state.excludedTags.size > 0)
    query.excludedTags = [...state.excludedTags].join(",");
  if (state.hostUsernames.size > 0)
    query.hosts = [...state.hostUsernames].join(",");
  if (state.createdFromUtc) query.createdFromUtc = state.createdFromUtc;
  if (state.createdToUtc) query.createdToUtc = state.createdToUtc;
  if (state.activatedFromUtc) query.activatedFromUtc = state.activatedFromUtc;
  if (state.activatedToUtc) query.activatedToUtc = state.activatedToUtc;
  if (state.closedFromUtc) query.closedFromUtc = state.closedFromUtc;
  if (state.closedToUtc) query.closedToUtc = state.closedToUtc;
  if (state.recruitmentStartedFromUtc)
    query.recruitmentStartedFromUtc = state.recruitmentStartedFromUtc;
  if (state.recruitmentStartedToUtc)
    query.recruitmentStartedToUtc = state.recruitmentStartedToUtc;
  if (state.sortBy !== "created") query.sortBy = state.sortBy;
  if (state.sortOrder !== "desc") query.sortOrder = state.sortOrder;

  return query;
}

// =============================================================================
// MODULE-LEVEL DISPATCHER (created once per module)
// =============================================================================

const dispatcher = createFilterDispatcher<GamesFilterState, GamesFilterAction>({
  buildQuery: buildQueryFromState,
  name: "useGamesFilter",
});

// =============================================================================
// COMPOSABLE
// =============================================================================

/**
 * Games filter composable with action-based state management.
 *
 * Architecture:
 * - URL is the single source of truth (filterState is computed from route.query)
 * - All mutations go through dispatch() which applies actions via pure reducer
 * - Actions batch via module-level debounce - rapid clicks merge into single navigation
 * - Pure reducer is testable in isolation without router
 */
export function useGamesFilter(): GamesFilterComposable {
  const route = useRoute();
  const router = useRouter();
  const { entitiesPerPage } = usePaging();

  // Set router for dispatcher
  dispatcher.setRouter(router);

  // Filter state derived from URL (single source of truth)
  const filterState = computed<GamesFilterState>(() =>
    parseQueryToState(route.query),
  );

  // Helper to get current state for dispatch
  const getCurrentState = () => filterState.value;

  // Dispatch helper
  const dispatch = (action: GamesFilterAction) =>
    dispatcher.dispatch(action, reducer, getCurrentState);

  // Convert to API params
  const searchParams = computed<GamesSearchParams>(() => {
    const state = filterState.value;
    const params: GamesSearchParams = {};

    if (state.search) params.search = state.search;

    if (state.status) {
      params.status = state.status;
      if (state.status === "Active" && state.recruitmentFilter !== "any") {
        params.recruitmentFilter = state.recruitmentFilter;
      }
      if (state.status === "Closed" && state.closedReasonFilter !== "any") {
        params.closedReasonFilter = state.closedReasonFilter as
          | "None"
          | "Finished"
          | "Frozen";
      }
    }

    if (state.requiredTags.size > 0)
      params.requiredTags = [...state.requiredTags];
    if (state.excludedTags.size > 0)
      params.excludedTags = [...state.excludedTags];
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
    if (state.recruitmentStartedFromUtc)
      params.recruitmentStartedFromUtc = dateToApiStart(
        state.recruitmentStartedFromUtc,
      );
    if (state.recruitmentStartedToUtc)
      params.recruitmentStartedToUtc = dateToApiEnd(
        state.recruitmentStartedToUtc,
      );

    params.sortBy = state.sortBy;
    params.sortOrder = state.sortOrder;

    const numberParam = route.query.number;
    if (numberParam) {
      const num = parseInt(String(numberParam), 10);
      if (!isNaN(num) && num > 0) params.number = num;
    }

    params.size = entitiesPerPage.value;
    return params;
  });

  // Active filters for UI display
  const activeFilters = computed(() => {
    const state = filterState.value;
    const filters: {
      key: string;
      label: string;
      type: "status" | "tag" | "host";
    }[] = [];

    if (state.status) {
      const option = STATUS_OPTIONS.find((o) => o.value === state.status);
      if (option)
        filters.push({
          key: `status-${state.status}`,
          label: option.label,
          type: "status",
        });
    }

    if (state.status === "Active" && state.recruitmentFilter !== "any") {
      const option = RECRUITMENT_FILTER_OPTIONS.find(
        (o) => o.value === state.recruitmentFilter,
      );
      if (option)
        filters.push({
          key: "recruitment",
          label: `Набор: ${option.label}`,
          type: "status",
        });
    }

    if (state.status === "Closed" && state.closedReasonFilter !== "any") {
      const option = CLOSED_REASON_FILTER_OPTIONS.find(
        (o) => o.value === state.closedReasonFilter,
      );
      if (option)
        filters.push({
          key: "closedReason",
          label: `Причина: ${option.label}`,
          type: "status",
        });
    }

    for (const tagId of state.requiredTags) {
      filters.push({
        key: `required-tag-${tagId}`,
        label: `+Тег #${tagId}`,
        type: "tag",
      });
    }
    for (const tagId of state.excludedTags) {
      filters.push({
        key: `excluded-tag-${tagId}`,
        label: `-Тег #${tagId}`,
        type: "tag",
      });
    }
    for (const username of state.hostUsernames) {
      filters.push({
        key: `host-${username}`,
        label: `ведущий: ${username}`,
        type: "host",
      });
    }

    return filters;
  });

  const hasActiveFilters = computed(() => {
    const state = filterState.value;
    return (
      state.search !== "" ||
      state.status !== null ||
      state.requiredTags.size > 0 ||
      state.excludedTags.size > 0 ||
      state.hostUsernames.size > 0 ||
      state.createdFromUtc !== null ||
      state.createdToUtc !== null ||
      state.activatedFromUtc !== null ||
      state.activatedToUtc !== null ||
      state.closedFromUtc !== null ||
      state.closedToUtc !== null ||
      state.recruitmentStartedFromUtc !== null ||
      state.recruitmentStartedToUtc !== null
    );
  });

  // ==========================================================================
  // ACTION DISPATCHERS (thin wrappers around dispatch)
  // ==========================================================================

  const setSearch = (search: string) =>
    dispatch({ type: "SET_SEARCH", search });
  const setStatus = (status: StatusValue | null) =>
    dispatch({ type: "SET_STATUS", status });
  const clearStatus = () => dispatch({ type: "SET_STATUS", status: null });
  const setRecruitmentFilter = (value: RecruitmentFilter) =>
    dispatch({ type: "SET_RECRUITMENT_FILTER", value });
  const setClosedReasonFilter = (value: ClosedReasonFilter) =>
    dispatch({ type: "SET_CLOSED_REASON_FILTER", value });
  const addRequiredTag = (tagId: number) =>
    dispatch({ type: "ADD_REQUIRED_TAG", tagId });
  const addExcludedTag = (tagId: number) =>
    dispatch({ type: "ADD_EXCLUDED_TAG", tagId });
  const removeTag = (tagId: number) => dispatch({ type: "REMOVE_TAG", tagId });
  const addHost = (username: string) =>
    dispatch({ type: "ADD_HOST", username });
  const removeHost = (username: string) =>
    dispatch({ type: "REMOVE_HOST", username });
  const clearHosts = () => dispatch({ type: "CLEAR_HOSTS" });
  const setCreatedRange = (fromUtc: string | null, toUtc: string | null) =>
    dispatch({ type: "SET_CREATED_RANGE", fromUtc, toUtc });
  const setActivatedRange = (fromUtc: string | null, toUtc: string | null) =>
    dispatch({ type: "SET_ACTIVATED_RANGE", fromUtc, toUtc });
  const setClosedRange = (fromUtc: string | null, toUtc: string | null) =>
    dispatch({ type: "SET_CLOSED_RANGE", fromUtc, toUtc });
  const setRecruitmentStartedRange = (
    fromUtc: string | null,
    toUtc: string | null,
  ) => dispatch({ type: "SET_RECRUITMENT_STARTED_RANGE", fromUtc, toUtc });
  const clearDateRanges = () => dispatch({ type: "CLEAR_DATE_RANGES" });
  const setSort = (sortBy: string, sortOrder?: "asc" | "desc") =>
    dispatch({ type: "SET_SORT", sortBy, sortOrder });
  const toggleSortOrder = () => dispatch({ type: "TOGGLE_SORT_ORDER" });
  const clearFilters = () => dispatch({ type: "CLEAR_FILTERS" });
  const validateTagFilters = (validTagIds: Set<number>) =>
    dispatch({ type: "VALIDATE_TAGS", validTagIds });

  return {
    filterState,
    searchParams,
    activeFilters,
    hasActiveFilters,
    setSearch,
    setStatus,
    clearStatus,
    setRecruitmentFilter,
    setClosedReasonFilter,
    addRequiredTag,
    addExcludedTag,
    removeTag,
    addHost,
    removeHost,
    clearHosts,
    setCreatedRange,
    setActivatedRange,
    setClosedRange,
    setRecruitmentStartedRange,
    clearDateRanges,
    setSort,
    toggleSortOrder,
    clearFilters,
    validateTagFilters,
  };
}
