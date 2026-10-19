/**
 * The sort step of every list filter reducer: SET_SORT without an order takes
 * the option's defaultDirection; the state is mutated in the reducer's clone.
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
