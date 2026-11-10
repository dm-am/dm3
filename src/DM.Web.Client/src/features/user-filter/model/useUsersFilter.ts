import { computed, type ComputedRef } from "vue";
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
import { UserRole, type UsersSearchParams } from "@/entities/user";
import type {
  UsersFilterState,
  UserActivityChoice,
  OnlineFilter,
  RoleFilter,
  ExperienceFilter,
} from "./types";
import {
  DEFAULT_FILTER_STATE,
  SORT_OPTIONS,
  EXPERIENCE_OPTIONS,
} from "./types";
import type { SortDirection } from "@/shared/lib/filters/types";

export interface UsersFilterComposable {
  filterState: ComputedRef<UsersFilterState>;
  searchParams: ComputedRef<UsersSearchParams>;
  hasActiveFilters: ComputedRef<boolean>;

  setSearch: (search: string) => void;
  setActivity: (activity: UserActivityChoice) => void;
  setOnlineFilter: (onlineFilter: OnlineFilter) => void;
  setRole: (role: RoleFilter) => void;
  setExperience: (experience: ExperienceFilter) => void;
  setRatingRange: (min: number | null, max: number | null) => void;
  setGamesHostingRange: (min: number | null, max: number | null) => void;
  setGamesPlayingRange: (min: number | null, max: number | null) => void;
  setBlogsHostingRange: (min: number | null, max: number | null) => void;
  setRegisteredRange: (fromUtc: string | null, toUtc: string | null) => void;
  setSort: (sortBy: string, sortOrder?: SortDirection) => void;
  toggleSortOrder: () => void;
  clearFilters: () => void;
}

type UsersFilterAction =
  | { type: "SET_SEARCH"; search: string }
  | { type: "SET_ACTIVITY"; activity: UserActivityChoice }
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
  | { type: "SET_SORT"; sortBy: string; sortOrder?: SortDirection }
  | { type: "TOGGLE_SORT_ORDER" }
  | { type: "CLEAR_FILTERS" };

function reducer(
  state: UsersFilterState,
  action: UsersFilterAction,
): UsersFilterState {
  const newState: UsersFilterState = { ...state };

  switch (action.type) {
    case "SET_SEARCH":
      newState.search = action.search;
      return newState;

    case "SET_ACTIVITY":
      newState.activity = action.activity;
      // The online filter is only relevant for active users.
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

    default:
      return reduceSortOrClear(
        newState,
        action,
        SORT_OPTIONS,
        createDefaultState,
        "asc",
      );
  }
}

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

function readCount(raw: LocationQuery[string] | undefined): number | null {
  if (!raw) return null;
  const value = parseInt(String(raw), 10);
  return isNaN(value) ? null : value;
}

function readChoice<T extends string>(
  raw: LocationQuery[string] | undefined,
  choices: Set<string>,
  fallback: T,
): T {
  if (!raw) return fallback;
  const value = String(raw);
  return choices.has(value) ? (value as T) : fallback;
}

// A zero is a count too.
function writeCount(
  query: Record<string, string>,
  key: string,
  value: number | null,
): void {
  if (value !== null) query[key] = String(value);
}

function parseQueryToState(query: LocationQuery): UsersFilterState {
  const state = createDefaultState();

  if (query.search) {
    state.search = String(query.search).slice(0, 200);
  }

  state.activity = readChoice(
    query.activity,
    validActivityValues,
    state.activity,
  );
  state.onlineFilter = readChoice(
    query.online,
    validOnlineValues,
    state.onlineFilter,
  );
  state.role = readChoice(query.role, validRoleValues, state.role);
  state.experience = readChoice(
    query.experience,
    validExperienceValues,
    state.experience,
  );

  state.ratingMin = readCount(query.ratingMin);
  state.ratingMax = readCount(query.ratingMax);
  state.gamesHostingMin = readCount(query.gamesHostingMin);
  state.gamesHostingMax = readCount(query.gamesHostingMax);
  state.gamesPlayingMin = readCount(query.gamesPlayingMin);
  state.gamesPlayingMax = readCount(query.gamesPlayingMax);
  state.blogsHostingMin = readCount(query.blogsHostingMin);
  state.blogsHostingMax = readCount(query.blogsHostingMax);

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

  if (state.activity !== def.activity) {
    query.activity = state.activity;
  }

  if (state.activity === "active" && state.onlineFilter !== def.onlineFilter) {
    query.online = state.onlineFilter;
  }

  if (state.role !== def.role) {
    query.role = state.role;
  }

  if (state.experience !== def.experience) {
    query.experience = state.experience;
  }

  writeCount(query, "ratingMin", state.ratingMin);
  writeCount(query, "ratingMax", state.ratingMax);
  writeCount(query, "gamesHostingMin", state.gamesHostingMin);
  writeCount(query, "gamesHostingMax", state.gamesHostingMax);
  writeCount(query, "gamesPlayingMin", state.gamesPlayingMin);
  writeCount(query, "gamesPlayingMax", state.gamesPlayingMax);
  writeCount(query, "blogsHostingMin", state.blogsHostingMin);
  writeCount(query, "blogsHostingMax", state.blogsHostingMax);

  if (state.registeredFromUtc)
    query.registeredFromUtc = state.registeredFromUtc;
  if (state.registeredToUtc) query.registeredToUtc = state.registeredToUtc;

  if (state.sortBy !== def.sortBy) query.sortBy = state.sortBy;
  if (state.sortOrder !== def.sortOrder) query.sortOrder = state.sortOrder;

  return query;
}

const dispatcher = createFilterDispatcher<UsersFilterState, UsersFilterAction>({
  buildQuery: buildQueryFromState,
  name: "useUsersFilter",
});

function usersSearchParams(
  state: UsersFilterState,
  pageNumber: number | undefined,
  size: number,
): UsersSearchParams {
  const params: UsersSearchParams = {};

  if (state.search) params.search = state.search;

  // Activity is always sent.
  params.activity = state.activity;

  if (state.activity === "active" && state.onlineFilter === "online") {
    params.isOnline = true;
  }

  if (state.role !== "all") {
    params.role = state.role as UserRole;
  }

  if (state.experience === "newbie") {
    params.isNewbie = true;
  } else if (state.experience === "experienced") {
    params.isNewbie = false;
  }

  if (state.ratingMin !== null) params.minRating = state.ratingMin;
  if (state.ratingMax !== null) params.maxRating = state.ratingMax;

  if (state.gamesHostingMin !== null)
    params.minGamesHosting = state.gamesHostingMin;
  if (state.gamesHostingMax !== null)
    params.maxGamesHosting = state.gamesHostingMax;

  if (state.gamesPlayingMin !== null)
    params.minGamesPlaying = state.gamesPlayingMin;
  if (state.gamesPlayingMax !== null)
    params.maxGamesPlaying = state.gamesPlayingMax;

  if (state.blogsHostingMin !== null)
    params.minBlogsHosting = state.blogsHostingMin;
  if (state.blogsHostingMax !== null)
    params.maxBlogsHosting = state.blogsHostingMax;

  if (state.registeredFromUtc)
    params.registeredFromUtc = dateToApiStart(state.registeredFromUtc);
  if (state.registeredToUtc)
    params.registeredToUtc = dateToApiEnd(state.registeredToUtc);

  params.sortBy = state.sortBy;
  params.sortOrder = state.sortOrder;

  if (pageNumber) params.number = pageNumber;

  params.size = size;
  return params;
}

// A sort alone does not filter.
function hasActiveUsersFilters(state: UsersFilterState): boolean {
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
}

// The URL is the single source of truth; actions batch through one module-level debounce.
export function useUsersFilter(): UsersFilterComposable {
  const route = useRoute();
  const router = useRouter();
  const { pageSize } = usePaging();

  dispatcher.setRouter(router);

  const filterState = computed<UsersFilterState>(() =>
    parseQueryToState(route.query),
  );

  const getCurrentState = () => filterState.value;

  const dispatch = (action: UsersFilterAction) =>
    dispatcher.dispatch(action, reducer, getCurrentState);

  const searchParams = computed(() =>
    usersSearchParams(
      filterState.value,
      parsePageNumber(route.query.number),
      pageSize.value,
    ),
  );

  const hasActiveFilters = computed(() =>
    hasActiveUsersFilters(filterState.value),
  );

  const setSearch = (search: string) =>
    dispatch({ type: "SET_SEARCH", search });
  const setActivity = (activity: UserActivityChoice) =>
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
  const { setSort, toggleSortOrder, clearFilters } =
    sortOrClearDispatchers(dispatch);

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
