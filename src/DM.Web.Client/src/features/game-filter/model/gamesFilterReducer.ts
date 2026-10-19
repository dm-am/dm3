import { applySortAction } from "@/shared/lib/filters";
import type {
  GamesFilterState,
  RecruitmentFilter,
  ClosedReasonFilter,
  StatusValue,
} from "./types";
import { SORT_OPTIONS } from "./types";
import { gameDateRange, type GameDateRangeKey } from "./gameDateRanges";

export type GamesFilterAction =
  | { type: "SET_SEARCH"; search: string }
  | { type: "SET_STATUS"; status: StatusValue | null }
  | { type: "SET_RECRUITMENT_FILTER"; value: RecruitmentFilter }
  | { type: "SET_CLOSED_REASON_FILTER"; value: ClosedReasonFilter }
  | { type: "ADD_REQUIRED_TAG"; tagId: number }
  | { type: "ADD_EXCLUDED_TAG"; tagId: number }
  | { type: "REMOVE_TAG"; tagId: number }
  | { type: "ADD_HOST"; username: string }
  | { type: "REMOVE_HOST"; username: string }
  | {
      type: "SET_DATE_RANGE";
      range: GameDateRangeKey;
      fromUtc: string | null;
      toUtc: string | null;
    }
  | { type: "SET_SORT"; sortBy: string; sortOrder?: "asc" | "desc" }
  | { type: "TOGGLE_SORT_ORDER" }
  | { type: "CLEAR_FILTERS" }
  | { type: "VALIDATE_TAGS"; validTagIds: Set<number> };

export function createDefaultState(): GamesFilterState {
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

export function reducer(
  state: GamesFilterState,
  action: GamesFilterAction,
): GamesFilterState {
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

    case "SET_DATE_RANGE": {
      const { from, to } = gameDateRange(action.range);
      newState[from] = action.fromUtc;
      newState[to] = action.toUtc;
      return newState;
    }

    case "SET_SORT":
    case "TOGGLE_SORT_ORDER":
      applySortAction(newState, action, SORT_OPTIONS, "asc");
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
      return changed ? newState : state;
    }

    default: {
      const _exhaustive: never = action;
      return _exhaustive;
    }
  }
}
