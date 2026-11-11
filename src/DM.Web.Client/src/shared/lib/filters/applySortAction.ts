/**
 * The sort and reset steps of every list filter reducer: SET_SORT without an
 * order takes the option's defaultDirection; the state is mutated in the
 * reducer's clone.
 */
import type { SortDirection, SortOption } from "./types";

type SortAction<TSortBy extends string> =
  | { type: "SET_SORT"; sortBy: TSortBy; sortOrder?: SortDirection }
  | { type: "TOGGLE_SORT_ORDER" };

type SortOrClearAction<TSortBy extends string> =
  SortAction<TSortBy> | { type: "CLEAR_FILTERS" };

export function applySortAction<TSortBy extends string>(
  state: { sortBy: TSortBy; sortOrder: SortDirection },
  action: SortAction<TSortBy>,
  sortOptions: readonly SortOption[],
  fallbackDirection: SortDirection = "desc",
): void {
  if (action.type === "SET_SORT") {
    state.sortBy = action.sortBy;
    if (action.sortOrder) {
      state.sortOrder = action.sortOrder;
    } else {
      const option = sortOptions.find((o) => o.value === action.sortBy);
      state.sortOrder = option?.defaultDirection || fallbackDirection;
    }
  } else {
    state.sortOrder = state.sortOrder === "asc" ? "desc" : "asc";
  }
}

/**
 * The default branch of a reducer's switch: what its own cases leave is the
 * sort or the reset, and an action it forgot fails the type check here.
 */
export function reduceSortOrClear<
  TSortBy extends string,
  TState extends { sortBy: TSortBy; sortOrder: SortDirection },
>(
  state: TState,
  action: SortOrClearAction<TSortBy>,
  sortOptions: readonly SortOption[],
  createDefault: () => TState,
  fallbackDirection?: SortDirection,
): TState {
  if (action.type === "CLEAR_FILTERS") return createDefault();
  applySortAction(state, action, sortOptions, fallbackDirection);
  return state;
}

/** The sort and reset dispatchers every list filter composable returns. */
export function sortOrClearDispatchers<TSortBy extends string>(
  dispatch: (action: SortOrClearAction<TSortBy>) => void,
) {
  return {
    setSort: (sortBy: TSortBy, sortOrder?: SortDirection) =>
      dispatch({ type: "SET_SORT", sortBy, sortOrder }),
    toggleSortOrder: () => dispatch({ type: "TOGGLE_SORT_ORDER" }),
    clearFilters: () => dispatch({ type: "CLEAR_FILTERS" }),
  };
}
