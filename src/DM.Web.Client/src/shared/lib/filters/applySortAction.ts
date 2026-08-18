/**
 * The sort step every list filter reducer shares.
 *
 * SET_SORT with an explicit order applies it as given; without one it looks up
 * the defaultDirection of the selected option, falling back when the field is
 * not among the options. TOGGLE_SORT_ORDER flips the current direction. The
 * helper mutates the state the reducer has already cloned, so the caller keeps
 * its "clone, mutate, return" shape.
 */
import type { SortDirection, SortOption } from "./types";

export function applySortAction<TSortBy extends string>(
  state: { sortBy: TSortBy; sortOrder: SortDirection },
  action:
    | { type: "SET_SORT"; sortBy: TSortBy; sortOrder?: SortDirection }
    | { type: "TOGGLE_SORT_ORDER" },
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
