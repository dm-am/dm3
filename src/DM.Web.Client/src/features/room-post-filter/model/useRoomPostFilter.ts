/**
 * The filter of one game room: the pulse filter moved into a room.
 *
 * Same URL spelling as the pulse (`q`, `sort`, `order`, `authors`,
 * `minRating`, `maxRating`, `createdFrom`, `createdTo`), plus the two things
 * only a room has to filter by: the characters the posts are written by and
 * whether a post has reviews. The default view is the room itself, in the
 * order it is written, and none of it is spelled in the address.
 */
import { computed, type ComputedRef } from "vue";
import { useRoute, useRouter, type LocationQuery } from "vue-router";
import { createFilterDispatcher } from "@/shared/lib/composables/createFilterDispatcher";
import {
  dateToApiEnd,
  dateToApiStart,
  isValidDate,
} from "@/shared/lib/filters/utils";
import type { RoomPostsFilter } from "@/entities/game";

export type RoomPostSortBy =
  | "created"
  | "rating"
  | "reviewcount"
  | "lastreview";
export type RoomPostReviews = "with" | "without";

/**
 * The "Без персонажа" entry of the characters set. Never a character id: ids
 * are GUIDs, so the word cannot collide with one.
 */
export const WITHOUT_CHARACTER = "none";

export interface RoomPostFilterState {
  search: string;
  sortBy: RoomPostSortBy;
  sortOrder: "asc" | "desc";
  authorUsernames: Set<string>;
  /** Character ids, and WITHOUT_CHARACTER for posts written without one. */
  characters: Set<string>;
  minRating: number | null;
  maxRating: number | null;
  reviews: RoomPostReviews | null;
  /** YYYY-MM-DD */
  createdFrom: string | null;
  /** YYYY-MM-DD */
  createdTo: string | null;
}

export interface RoomPostFilterComposable {
  filterState: ComputedRef<RoomPostFilterState>;
  /** What the room list is asked on top of the page; empty for the room itself. */
  apiFilter: ComputedRef<RoomPostsFilter>;
  /** A filter narrows the posts (sort left out: it reorders, it does not narrow). */
  hasActiveFilters: ComputedRef<boolean>;
  /**
   * Anything but the room as it is written: a filter or another order. Such a
   * view is not reading the room, so it does not mark the room read, and a
   * post's own page is somewhere else.
   */
  isFilteredView: ComputedRef<boolean>;

  setSearch: (search: string) => void;
  setSort: (sortBy: RoomPostSortBy, sortOrder?: "asc" | "desc") => void;
  addAuthor: (username: string) => void;
  removeAuthor: (username: string) => void;
  addCharacter: (id: string) => void;
  removeCharacter: (id: string) => void;
  setRatingRange: (min: number | null, max: number | null) => void;
  setReviews: (reviews: RoomPostReviews | null) => void;
  setCreatedRange: (from: string | null, to: string | null) => void;
  clearFilters: () => void;
}

type Action =
  | { type: "SET_SEARCH"; search: string }
  | { type: "SET_SORT"; sortBy: RoomPostSortBy; sortOrder?: "asc" | "desc" }
  | { type: "ADD_AUTHOR"; username: string }
  | { type: "REMOVE_AUTHOR"; username: string }
  | { type: "ADD_CHARACTER"; id: string }
  | { type: "REMOVE_CHARACTER"; id: string }
  | { type: "SET_RATING_RANGE"; min: number | null; max: number | null }
  | { type: "SET_REVIEWS"; reviews: RoomPostReviews | null }
  | { type: "SET_CREATED_RANGE"; from: string | null; to: string | null }
  | { type: "CLEAR_FILTERS" };

const DEFAULT_SORT_BY: RoomPostSortBy = "created";
const DEFAULT_SORT_ORDER = "asc";

/**
 * The direction a sort takes when it is picked: the room reads forward, every
 * other order puts the most first.
 */
export function defaultSortOrder(sortBy: RoomPostSortBy): "asc" | "desc" {
  return sortBy === "created" ? "asc" : "desc";
}

function createDefaultState(): RoomPostFilterState {
  return {
    search: "",
    sortBy: DEFAULT_SORT_BY,
    sortOrder: DEFAULT_SORT_ORDER,
    authorUsernames: new Set(),
    characters: new Set(),
    minRating: null,
    maxRating: null,
    reviews: null,
    createdFrom: null,
    createdTo: null,
  };
}

function withSet(
  state: RoomPostFilterState,
  key: "authorUsernames" | "characters",
  value: string,
  add: boolean,
): RoomPostFilterState {
  if (state[key].has(value) === add) return state;
  const next = new Set(state[key]);
  if (add) next.add(value);
  else next.delete(value);
  return { ...state, [key]: next };
}

// Returns the same state when nothing changed, which the dispatcher reads as
// "no navigation needed".
function reducer(
  state: RoomPostFilterState,
  action: Action,
): RoomPostFilterState {
  switch (action.type) {
    case "SET_SEARCH":
      return action.search === state.search
        ? state
        : { ...state, search: action.search };

    case "SET_SORT": {
      const sortOrder = action.sortOrder ?? defaultSortOrder(action.sortBy);
      if (action.sortBy === state.sortBy && sortOrder === state.sortOrder)
        return state;
      return { ...state, sortBy: action.sortBy, sortOrder };
    }

    case "ADD_AUTHOR":
      return withSet(state, "authorUsernames", action.username, true);
    case "REMOVE_AUTHOR":
      return withSet(state, "authorUsernames", action.username, false);
    case "ADD_CHARACTER":
      return withSet(state, "characters", action.id, true);
    case "REMOVE_CHARACTER":
      return withSet(state, "characters", action.id, false);

    case "SET_RATING_RANGE":
      if (action.min === state.minRating && action.max === state.maxRating)
        return state;
      return { ...state, minRating: action.min, maxRating: action.max };

    case "SET_REVIEWS":
      return action.reviews === state.reviews
        ? state
        : { ...state, reviews: action.reviews };

    case "SET_CREATED_RANGE":
      if (action.from === state.createdFrom && action.to === state.createdTo)
        return state;
      return { ...state, createdFrom: action.from, createdTo: action.to };

    case "CLEAR_FILTERS":
      return isDefault(state) ? state : createDefaultState();

    default: {
      const _exhaustive: never = action;
      return _exhaustive;
    }
  }
}

function isDefault(state: RoomPostFilterState): boolean {
  return (
    !hasFilters(state) &&
    state.sortBy === DEFAULT_SORT_BY &&
    state.sortOrder === DEFAULT_SORT_ORDER
  );
}

function hasFilters(state: RoomPostFilterState): boolean {
  return (
    state.search !== "" ||
    state.authorUsernames.size > 0 ||
    state.characters.size > 0 ||
    state.minRating !== null ||
    state.maxRating !== null ||
    state.reviews !== null ||
    state.createdFrom !== null ||
    state.createdTo !== null
  );
}

const SORT_FIELDS = new Set<string>([
  "created",
  "rating",
  "reviewcount",
  "lastreview",
]);

function parseList(value: LocationQuery[string]): string[] {
  if (!value) return [];
  return String(value)
    .split(",")
    .map((s) => s.trim())
    .filter(Boolean);
}

function parseInteger(value: LocationQuery[string]): number | null {
  if (value === undefined || value === null || value === "") return null;
  const parsed = parseInt(String(value), 10);
  return isNaN(parsed) ? null : parsed;
}

/** Exported for the spec: the address is the whole state. */
export function parseQueryToState(query: LocationQuery): RoomPostFilterState {
  const state = createDefaultState();

  if (query.q) state.search = String(query.q).slice(0, 200);

  const sort = query.sort ? String(query.sort) : "";
  if (SORT_FIELDS.has(sort)) state.sortBy = sort as RoomPostSortBy;
  const order = query.order ? String(query.order) : "";
  if (order === "asc" || order === "desc") state.sortOrder = order;

  state.authorUsernames = new Set(parseList(query.authors));
  state.characters = new Set(parseList(query.characters));
  state.minRating = parseInteger(query.minRating);
  state.maxRating = parseInteger(query.maxRating);

  const reviews = query.reviews ? String(query.reviews) : "";
  if (reviews === "with" || reviews === "without") state.reviews = reviews;

  const from = query.createdFrom ? String(query.createdFrom) : "";
  if (isValidDate(from)) state.createdFrom = from;
  const to = query.createdTo ? String(query.createdTo) : "";
  if (isValidDate(to)) state.createdTo = to;

  return state;
}

/** Exported for the spec: only what differs from the room itself is written. */
export function buildQueryFromState(
  state: RoomPostFilterState,
): Record<string, string> {
  const query: Record<string, string> = {};

  if (state.search) query.q = state.search;
  if (state.sortBy !== DEFAULT_SORT_BY) query.sort = state.sortBy;
  if (state.sortOrder !== DEFAULT_SORT_ORDER) query.order = state.sortOrder;
  if (state.authorUsernames.size > 0)
    query.authors = [...state.authorUsernames].join(",");
  if (state.characters.size > 0)
    query.characters = [...state.characters].join(",");
  if (state.minRating !== null) query.minRating = String(state.minRating);
  if (state.maxRating !== null) query.maxRating = String(state.maxRating);
  if (state.reviews) query.reviews = state.reviews;
  if (state.createdFrom) query.createdFrom = state.createdFrom;
  if (state.createdTo) query.createdTo = state.createdTo;

  return query;
}

/**
 * Exported for the spec: the request the list is asked with. The room itself
 * asks with nothing at all, so its request is the one it always was, and the
 * server numbers its page without the extra query a filtered view costs.
 */
export function buildApiFilter(state: RoomPostFilterState): RoomPostsFilter {
  const filter: RoomPostsFilter = {};

  if (state.search) filter.search = state.search;
  if (
    state.sortBy !== DEFAULT_SORT_BY ||
    state.sortOrder !== DEFAULT_SORT_ORDER
  ) {
    filter.sortBy = state.sortBy;
    filter.sortOrder = state.sortOrder;
  }
  if (state.authorUsernames.size > 0)
    filter.authorUsernames = [...state.authorUsernames];

  const characterIds = [...state.characters].filter(
    (id) => id !== WITHOUT_CHARACTER,
  );
  if (characterIds.length > 0) filter.characterIds = characterIds;
  if (state.characters.has(WITHOUT_CHARACTER)) filter.withoutCharacter = true;

  if (state.minRating !== null) filter.minRating = state.minRating;
  if (state.maxRating !== null) filter.maxRating = state.maxRating;
  if (state.reviews) filter.hasReviews = state.reviews === "with";

  const from = dateToApiStart(state.createdFrom);
  if (from) filter.createdFromUtc = from;
  const to = dateToApiEnd(state.createdTo);
  if (to) filter.createdToUtc = to;

  return filter;
}

const dispatcher = createFilterDispatcher<RoomPostFilterState, Action>({
  buildQuery: buildQueryFromState,
  name: "useRoomPostFilter",
});

export function useRoomPostFilter(): RoomPostFilterComposable {
  const route = useRoute();
  dispatcher.setRouter(useRouter());

  const filterState = computed(() => parseQueryToState(route.query));
  const dispatch = (action: Action) =>
    dispatcher.dispatch(action, reducer, () => filterState.value);

  return {
    filterState,
    apiFilter: computed(() => buildApiFilter(filterState.value)),
    hasActiveFilters: computed(() => hasFilters(filterState.value)),
    isFilteredView: computed(() => !isDefault(filterState.value)),

    setSearch: (search) => dispatch({ type: "SET_SEARCH", search }),
    setSort: (sortBy, sortOrder) =>
      dispatch({ type: "SET_SORT", sortBy, sortOrder }),
    addAuthor: (username) => dispatch({ type: "ADD_AUTHOR", username }),
    removeAuthor: (username) => dispatch({ type: "REMOVE_AUTHOR", username }),
    addCharacter: (id) => dispatch({ type: "ADD_CHARACTER", id }),
    removeCharacter: (id) => dispatch({ type: "REMOVE_CHARACTER", id }),
    setRatingRange: (min, max) =>
      dispatch({ type: "SET_RATING_RANGE", min, max }),
    setReviews: (reviews) => dispatch({ type: "SET_REVIEWS", reviews }),
    setCreatedRange: (from, to) =>
      dispatch({ type: "SET_CREATED_RANGE", from, to }),
    clearFilters: () => dispatch({ type: "CLEAR_FILTERS" }),
  };
}
