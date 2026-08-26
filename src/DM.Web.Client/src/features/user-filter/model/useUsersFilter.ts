import { computed, type ComputedRef } from "vue";
import { useRoute, useRouter } from "vue-router";
import type { LocationQuery } from "vue-router";
import { usePaging } from "@/shared/lib/composables/usePaging";
import { createFilterDispatcher } from "@/shared/lib/composables/createFilterDispatcher";
import {
  applySortAction,
  parseDateFromUrl,
  dateToApiStart,
  dateToApiEnd,
  toQueryValue,
  parsePageNumber,
} from "@/shared/lib/filters";
import { UserRole } from "@/entities/user";
import type {
  UsersFilterState,
  UsersSearchParams,
  ActivityFilter,
  OnlineFilter,
  RoleFilter,
  ExperienceFilter,
} from "./types";
import {
  DEFAULT_FILTER_STATE,
  SORT_OPTIONS,
  EXPERIENCE_OPTIONS,
} from "./types";

// =============================================================================
// RETURN TYPE
// =============================================================================

export interface UsersFilterComposable {
  /** Current filter state (computed from URL) */
  filterState: ComputedRef<UsersFilterState>;
  /** API search parameters (computed from filterState) */
  searchParams: ComputedRef<UsersSearchParams>;
  /** Whether any filters are active */
  hasActiveFilters: ComputedRef<boolean>;

  // Actions
  setSearch: (search: string) => void;
  setActivity: (activity: ActivityFilter) => void;
  setOnlineFilter: (onlineFilter: OnlineFilter) => void;
  setRole: (role: RoleFilter) => void;
  setExperience: (experience: ExperienceFilter) => void;
  setRatingRange: (min: number | null, max: number | null) => void;
  setGamesHostingRange: (min: number | null, max: number | null) => void;
  setGamesPlayingRange: (min: number | null, max: number | null) => void;
  setBlogsHostingRange: (min: number | null, max: number | null) => void;
  setRegisteredRange: (fromUtc: string | null, toUtc: string | null) => void;
  setSort: (sortBy: string, sortOrder?: "asc" | "desc") => void;
  toggleSortOrder: () => void;
  clearFilters: () => void;
}

// =============================================================================
// ACTION TYPES (Discriminated Union)
// =============================================================================

type UsersFilterAction =
  | { type: "SET_SEARCH"; search: string }
  | { type: "SET_ACTIVITY"; activity: ActivityFilter }
  | { type: "SET_ONLINE_FILTER"; onlineFilter: OnlineFilter }
  | { type: "SET_ROLE"; role: RoleFilter }
  | { type: "SET_EXPERIENCE"; experience: ExperienceFilter }
  | { type: "SET_RATING_RANGE"; min: number | null; max: number | null }
  | { type: "SET_GAMES_HOSTING_RANGE"; min: number | null; max: number | null }
  | { type: "SET_GAMES_PLAYING_RANGE"; min: number | null; max: number | null }
  | { type: "SET_BLOGS_HOSTING_RANGE"; min: number | null; max: number | null }
  | {
      type: "SET_REGISTERED_RANGE";
      fromUtc: string | null;
      toUtc: string | null;
    }
  | { type: "SET_SORT"; sortBy: string; sortOrder?: "asc" | "desc" }
  | { type: "TOGGLE_SORT_ORDER" }
  | { type: "CLEAR_FILTERS" };

// =============================================================================
// PURE REDUCER (Testable, no side effects)
// =============================================================================

/**
 * Pure reducer function - applies action to state and returns new state.
 * This is the ONLY place where state transformation logic lives.
 */
function reducer(
  state: UsersFilterState,
  action: UsersFilterAction,
): UsersFilterState {
  // Clone state to avoid mutation
  const newState: UsersFilterState = { ...state };

  switch (action.type) {
    case "SET_SEARCH":
      newState.search = action.search;
      return newState;

    case "SET_ACTIVITY":
      newState.activity = action.activity;
      // Reset online filter when changing activity (only relevant for active)
      if (action.activity !== "active") {
        newState.onlineFilter = "all";
      }
      return newState;

    case "SET_ONLINE_FILTER":
      newState.onlineFilter = action.onlineFilter;
      return newState;

    case "SET_ROLE":
      newState.role = action.role;
      return newState;

    case "SET_EXPERIENCE":
      newState.experience = action.experience;
      return newState;

    case "SET_RATING_RANGE":
      newState.ratingMin = action.min;
      newState.ratingMax = action.max;
      return newState;

    case "SET_GAMES_HOSTING_RANGE":
      newState.gamesHostingMin = action.min;
      newState.gamesHostingMax = action.max;
      return newState;

    case "SET_GAMES_PLAYING_RANGE":
      newState.gamesPlayingMin = action.min;
      newState.gamesPlayingMax = action.max;
      return newState;

    case "SET_BLOGS_HOSTING_RANGE":
      newState.blogsHostingMin = action.min;
      newState.blogsHostingMax = action.max;
      return newState;

    case "SET_REGISTERED_RANGE":
      newState.registeredFromUtc = action.fromUtc;
      newState.registeredToUtc = action.toUtc;
      return newState;

    case "SET_SORT":
    case "TOGGLE_SORT_ORDER":
      applySortAction(newState, action, SORT_OPTIONS, "asc");
      return newState;

    case "CLEAR_FILTERS":
      return createDefaultState();

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
const validActivityValues = new Set<string>(["active", "inactive", "all"]);
const validOnlineValues = new Set<string>(["all", "online"]);
const validRoleValues = new Set<string>(["all", ...Object.values(UserRole)]);
const validExperienceValues = new Set<string>(
  EXPERIENCE_OPTIONS.map((o) => o.value),
);

function createDefaultState(): UsersFilterState {
  return { ...DEFAULT_FILTER_STATE };
}

function parseQueryToState(query: LocationQuery): UsersFilterState {
  const state = createDefaultState();

  if (query.search) {
    state.search = String(query.search).slice(0, 200);
  }

  if (query.activity) {
    const activity = String(query.activity);
    if (validActivityValues.has(activity)) {
      state.activity = activity as ActivityFilter;
    }
  }

  if (query.online) {
    const online = String(query.online);
    if (validOnlineValues.has(online)) {
      state.onlineFilter = online as OnlineFilter;
    }
  }

  if (query.role) {
    const role = String(query.role);
    if (validRoleValues.has(role)) {
      state.role = role as RoleFilter;
    }
  }

  if (query.experience) {
    const experience = String(query.experience);
    if (validExperienceValues.has(experience)) {
      state.experience = experience as ExperienceFilter;
    }
  }

  if (query.ratingMin) {
    const val = parseInt(String(query.ratingMin), 10);
    if (!isNaN(val)) state.ratingMin = val;
  }
  if (query.ratingMax) {
    const val = parseInt(String(query.ratingMax), 10);
    if (!isNaN(val)) state.ratingMax = val;
  }

  if (query.gamesHostingMin) {
    const val = parseInt(String(query.gamesHostingMin), 10);
    if (!isNaN(val)) state.gamesHostingMin = val;
  }
  if (query.gamesHostingMax) {
    const val = parseInt(String(query.gamesHostingMax), 10);
    if (!isNaN(val)) state.gamesHostingMax = val;
  }

  if (query.gamesPlayingMin) {
    const val = parseInt(String(query.gamesPlayingMin), 10);
    if (!isNaN(val)) state.gamesPlayingMin = val;
  }
  if (query.gamesPlayingMax) {
    const val = parseInt(String(query.gamesPlayingMax), 10);
    if (!isNaN(val)) state.gamesPlayingMax = val;
  }

  if (query.blogsHostingMin) {
    const val = parseInt(String(query.blogsHostingMin), 10);
    if (!isNaN(val)) state.blogsHostingMin = val;
  }
  if (query.blogsHostingMax) {
    const val = parseInt(String(query.blogsHostingMax), 10);
    if (!isNaN(val)) state.blogsHostingMax = val;
  }

  state.registeredFromUtc = parseDateFromUrl(
    toQueryValue(query.registeredFromUtc),
  );
  state.registeredToUtc = parseDateFromUrl(toQueryValue(query.registeredToUtc));

  const sortByRaw = query.sortBy as string;
  if (validSortByValues.has(sortByRaw)) state.sortBy = sortByRaw;

  const sortOrderRaw = query.sortOrder as string;
  if (sortOrderRaw === "asc" || sortOrderRaw === "desc")
    state.sortOrder = sortOrderRaw;

  return state;
}

function buildQueryFromState(state: UsersFilterState): Record<string, string> {
  const query: Record<string, string> = {};
  const def = DEFAULT_FILTER_STATE;

  if (state.search) query.search = state.search;

  // Activity (only if non-default)
  if (state.activity !== def.activity) {
    query.activity = state.activity;
  }

  // Online (only if activity is active and online is non-default)
  if (state.activity === "active" && state.onlineFilter !== def.onlineFilter) {
    query.online = state.onlineFilter;
  }

  // Role
  if (state.role !== def.role) {
    query.role = state.role;
  }

  // Experience (if non-default)
  if (state.experience !== def.experience) {
    query.experience = state.experience;
  }

  // Rating range (if set)
  if (state.ratingMin !== null) query.ratingMin = String(state.ratingMin);
  if (state.ratingMax !== null) query.ratingMax = String(state.ratingMax);

  // Games hosting range
  if (state.gamesHostingMin !== null)
    query.gamesHostingMin = String(state.gamesHostingMin);
  if (state.gamesHostingMax !== null)
    query.gamesHostingMax = String(state.gamesHostingMax);

  // Games playing range
  if (state.gamesPlayingMin !== null)
    query.gamesPlayingMin = String(state.gamesPlayingMin);
  if (state.gamesPlayingMax !== null)
    query.gamesPlayingMax = String(state.gamesPlayingMax);

  // Blogs hosting range
  if (state.blogsHostingMin !== null)
    query.blogsHostingMin = String(state.blogsHostingMin);
  if (state.blogsHostingMax !== null)
    query.blogsHostingMax = String(state.blogsHostingMax);

  // Date range
  if (state.registeredFromUtc)
    query.registeredFromUtc = state.registeredFromUtc;
  if (state.registeredToUtc) query.registeredToUtc = state.registeredToUtc;

  // Sort (only if non-default)
  if (state.sortBy !== def.sortBy) query.sortBy = state.sortBy;
  if (state.sortOrder !== def.sortOrder) query.sortOrder = state.sortOrder;

  return query;
}

// =============================================================================
// MODULE-LEVEL DISPATCHER (created once per module)
// =============================================================================

const dispatcher = createFilterDispatcher<UsersFilterState, UsersFilterAction>({
  buildQuery: buildQueryFromState,
  name: "useUsersFilter",
});

// =============================================================================
// COMPOSABLE
// =============================================================================

/**
 * Users filter composable with action-based state management.
 *
 * Architecture:
 * - URL is the single source of truth (filterState is computed from route.query)
 * - All mutations go through dispatch() which applies actions via pure reducer
 * - Actions batch via module-level debounce - rapid clicks merge into single navigation
 * - Pure reducer is testable in isolation without router
 */
export function useUsersFilter(): UsersFilterComposable {
  const route = useRoute();
  const router = useRouter();
  const { entitiesPerPage } = usePaging();

  // Set router for dispatcher
  dispatcher.setRouter(router);

  // Filter state derived from URL (single source of truth)
  const filterState = computed<UsersFilterState>(() =>
    parseQueryToState(route.query),
  );

  // Helper to get current state for dispatch
  const getCurrentState = () => filterState.value;

  // Dispatch helper
  const dispatch = (action: UsersFilterAction) =>
    dispatcher.dispatch(action, reducer, getCurrentState);

  // Convert to API params
  const searchParams = computed<UsersSearchParams>(() => {
    const state = filterState.value;
    const params: UsersSearchParams = {};

    if (state.search) params.search = state.search;

    // Activity filter (always send)
    params.activity = state.activity;

    // Online filter (only when activity is "active" and online is selected)
    if (state.activity === "active" && state.onlineFilter === "online") {
      params.isOnline = true;
    }

    // Role
    if (state.role !== "all") {
      params.role = state.role as UserRole;
    }

    // Experience filter (applies to all roles)
    if (state.experience === "newbie") {
      params.isNewbie = true;
    } else if (state.experience === "experienced") {
      params.isNewbie = false;
    }

    // Rating range
    if (state.ratingMin !== null) params.minRating = state.ratingMin;
    if (state.ratingMax !== null) params.maxRating = state.ratingMax;

    // Games hosting range
    if (state.gamesHostingMin !== null)
      params.minGamesHosting = state.gamesHostingMin;
    if (state.gamesHostingMax !== null)
      params.maxGamesHosting = state.gamesHostingMax;

    // Games playing range
    if (state.gamesPlayingMin !== null)
      params.minGamesPlaying = state.gamesPlayingMin;
    if (state.gamesPlayingMax !== null)
      params.maxGamesPlaying = state.gamesPlayingMax;

    // Blogs hosting range
    if (state.blogsHostingMin !== null)
      params.minBlogsHosting = state.blogsHostingMin;
    if (state.blogsHostingMax !== null)
      params.maxBlogsHosting = state.blogsHostingMax;

    // Date range
    if (state.registeredFromUtc)
      params.registeredFromUtc = dateToApiStart(state.registeredFromUtc);
    if (state.registeredToUtc)
      params.registeredToUtc = dateToApiEnd(state.registeredToUtc);

    // Sort
    params.sortBy = state.sortBy;
    params.sortOrder = state.sortOrder;

    // Page number (from URL query directly)
    const pageNumber = parsePageNumber(route.query.number);
    if (pageNumber) params.number = pageNumber;

    params.size = entitiesPerPage.value;
    return params;
  });

  // Active filters check
  const hasActiveFilters = computed(() => {
    const state = filterState.value;
    const def = DEFAULT_FILTER_STATE;
    return (
      state.search !== def.search ||
      state.activity !== def.activity ||
      state.onlineFilter !== def.onlineFilter ||
      state.role !== def.role ||
      state.experience !== def.experience ||
      state.ratingMin !== def.ratingMin ||
      state.ratingMax !== def.ratingMax ||
      state.gamesHostingMin !== def.gamesHostingMin ||
      state.gamesHostingMax !== def.gamesHostingMax ||
      state.gamesPlayingMin !== def.gamesPlayingMin ||
      state.gamesPlayingMax !== def.gamesPlayingMax ||
      state.blogsHostingMin !== def.blogsHostingMin ||
      state.blogsHostingMax !== def.blogsHostingMax ||
      state.registeredFromUtc !== def.registeredFromUtc ||
      state.registeredToUtc !== def.registeredToUtc
    );
  });

  // ==========================================================================
  // ACTION DISPATCHERS (thin wrappers around dispatch)
  // ==========================================================================

  const setSearch = (search: string) =>
    dispatch({ type: "SET_SEARCH", search });
  const setActivity = (activity: ActivityFilter) =>
    dispatch({ type: "SET_ACTIVITY", activity });
  const setOnlineFilter = (onlineFilter: OnlineFilter) =>
    dispatch({ type: "SET_ONLINE_FILTER", onlineFilter });
  const setRole = (role: RoleFilter) => dispatch({ type: "SET_ROLE", role });
  const setExperience = (experience: ExperienceFilter) =>
    dispatch({ type: "SET_EXPERIENCE", experience });
  const setRatingRange = (min: number | null, max: number | null) =>
    dispatch({ type: "SET_RATING_RANGE", min, max });
  const setGamesHostingRange = (min: number | null, max: number | null) =>
    dispatch({ type: "SET_GAMES_HOSTING_RANGE", min, max });
  const setGamesPlayingRange = (min: number | null, max: number | null) =>
    dispatch({ type: "SET_GAMES_PLAYING_RANGE", min, max });
  const setBlogsHostingRange = (min: number | null, max: number | null) =>
    dispatch({ type: "SET_BLOGS_HOSTING_RANGE", min, max });
  const setRegisteredRange = (fromUtc: string | null, toUtc: string | null) =>
    dispatch({ type: "SET_REGISTERED_RANGE", fromUtc, toUtc });
  const setSort = (sortBy: string, sortOrder?: "asc" | "desc") =>
    dispatch({ type: "SET_SORT", sortBy, sortOrder });
  const toggleSortOrder = () => dispatch({ type: "TOGGLE_SORT_ORDER" });
  const clearFilters = () => dispatch({ type: "CLEAR_FILTERS" });

  return {
    filterState,
    searchParams,
    hasActiveFilters,
    setSearch,
    setActivity,
    setOnlineFilter,
    setRole,
    setExperience,
    setRatingRange,
    setGamesHostingRange,
    setGamesPlayingRange,
    setBlogsHostingRange,
    setRegisteredRange,
    setSort,
    toggleSortOrder,
    clearFilters,
  };
}
