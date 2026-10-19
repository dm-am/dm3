import type { LocationQuery } from "vue-router";
import { parseDateFromUrl, toQueryValue } from "@/shared/lib/filters";
import type {
  GamesFilterState,
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
import { createDefaultState } from "./gamesFilterReducer";
import { GAME_DATE_RANGES } from "./gameDateRanges";

const validSortByValues = new Set<string>(SORT_OPTIONS.map((o) => o.value));
const validStatusValues = new Set<string>(STATUS_OPTIONS.map((o) => o.value));
const validRecruitmentValues = new Set<string>(
  RECRUITMENT_FILTER_OPTIONS.map((o) => o.value),
);
const validClosedReasonValues = new Set<string>(
  CLOSED_REASON_FILTER_OPTIONS.map((o) => o.value),
);

function addTagIds(into: Set<number>, raw: LocationQuery[string]) {
  if (!raw) return;
  for (const tag of String(raw).split(",")) {
    const tagId = parseInt(tag, 10);
    if (!isNaN(tagId) && tagId > 0) into.add(tagId);
  }
}

export function parseQueryToState(query: LocationQuery): GamesFilterState {
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

  addTagIds(state.requiredTags, query.requiredTags);
  addTagIds(state.excludedTags, query.excludedTags);

  if (query.hosts) {
    for (const host of String(query.hosts).split(",")) {
      const trimmed = host.trim().slice(0, 20);
      if (trimmed) state.hostUsernames.add(trimmed);
    }
  }

  for (const { from, to } of GAME_DATE_RANGES) {
    state[from] = parseDateFromUrl(toQueryValue(query[from]));
    state[to] = parseDateFromUrl(toQueryValue(query[to]));
  }

  const sortByRaw = query.sortBy as string;
  if (validSortByValues.has(sortByRaw)) state.sortBy = sortByRaw;

  const sortOrderRaw = query.sortOrder as string;
  if (sortOrderRaw === "asc" || sortOrderRaw === "desc")
    state.sortOrder = sortOrderRaw;

  return state;
}

// Only what differs from the default; the dates one by one, in the order the address has always carried them.
export function buildQueryFromState(
  state: GamesFilterState,
): Record<string, string> {
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
