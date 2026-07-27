import { defineStore } from "pinia";
import { computed, ref } from "vue";
import type { MessageSearchResult, SearchScope, SearchSort } from "./types";
import messageSearchApi from "../api/messageSearchApi";

const PAGE_SIZE = 50;

/**
 * Map a scope selection to the repeatable `in` query param.
 *
 * Pure and exported for unit testing. Returns `undefined` for the "search
 * everything" cases (all scope, or a dm/game scope with no concrete target
 * picked yet — the backend `in:` filter needs a specific chat/game id).
 */
export function scopeToIn(scope: SearchScope): string[] | undefined {
  switch (scope.kind) {
    case "global":
      return ["global"];
    case "dm":
      return scope.targetId ? [`dm:${scope.targetId}`] : undefined;
    case "game":
      return scope.targetId ? [`game:${scope.targetId}`] : undefined;
    case "all":
    default:
      return undefined;
  }
}

/** Map the UI sort toggle to the optional `sort` query param. */
export function sortToParam(sort: SearchSort): string {
  return sort === "best" ? "best" : "newest";
}

/**
 * A dm/game scope is only actionable once a concrete target is chosen; the
 * panel blocks the search until then.
 */
export function scopeNeedsTarget(scope: SearchScope): boolean {
  return (scope.kind === "dm" || scope.kind === "game") && !scope.targetId;
}

/**
 * Dedicated search store. Deliberately NOT part of the live global-chat store:
 * search results are a distinct navigable list with their own cursor/sort
 * state, not the live scroll-back buffer.
 */
export const useMessageSearchStore = defineStore("messageSearch", () => {
  // Query + controls
  const query = ref("");
  const scope = ref<SearchScope>({ kind: "all" });
  const sort = ref<SearchSort>("date");

  // Results + cursor
  const results = ref<MessageSearchResult[]>([]);
  const nextCursor = ref<string | null>(null);
  const hasMore = ref(false);

  // Status
  const loading = ref(false);
  const loadingMore = ref(false);
  const error = ref<string | null>(null);
  /** True once a search has been dispatched — distinguishes the initial
   * prompt state from a genuine "no results" state. */
  const hasSearched = ref(false);

  const trimmedQuery = computed(() => query.value.trim());

  /** Whether a search can currently run (non-empty query, resolvable scope). */
  const canSearch = computed(
    () => trimmedQuery.value.length > 0 && !scopeNeedsTarget(scope.value),
  );

  function reset() {
    results.value = [];
    nextCursor.value = null;
    hasMore.value = false;
    error.value = null;
    hasSearched.value = false;
    loading.value = false;
    loadingMore.value = false;
  }

  function setScope(next: SearchScope) {
    scope.value = next;
  }

  function setSort(next: SearchSort) {
    sort.value = next;
  }

  /** Run a fresh search from the top (replaces results). */
  async function search() {
    if (!canSearch.value) {
      reset();
      return;
    }
    loading.value = true;
    error.value = null;
    hasSearched.value = true;
    try {
      const { data, error: apiError } = await messageSearchApi.searchMessages({
        q: trimmedQuery.value,
        in: scopeToIn(scope.value),
        sort: sortToParam(sort.value),
        limit: PAGE_SIZE,
      });
      if (apiError) {
        results.value = [];
        nextCursor.value = null;
        hasMore.value = false;
        error.value = "Не удалось выполнить поиск";
        return;
      }
      results.value = data?.resources ?? [];
      nextCursor.value = data?.paging?.nextCursor ?? null;
      hasMore.value = data?.paging?.hasNext ?? false;
    } finally {
      loading.value = false;
    }
  }

  /** Load the next (older) page and append it to the results. */
  async function loadMore() {
    if (loadingMore.value || !hasMore.value || !nextCursor.value) return;
    loadingMore.value = true;
    error.value = null;
    try {
      const { data, error: apiError } = await messageSearchApi.searchMessages({
        q: trimmedQuery.value,
        in: scopeToIn(scope.value),
        sort: sortToParam(sort.value),
        cursor: nextCursor.value,
        limit: PAGE_SIZE,
      });
      if (apiError) {
        // Keep the already-rendered results and the cursor so the sentinel's
        // retry can try again, matching the chat's stale-content convention.
        error.value = "Не удалось загрузить еще";
        return;
      }
      if (data && data.resources.length > 0) {
        results.value = [...results.value, ...data.resources];
        nextCursor.value = data.paging?.nextCursor ?? null;
        hasMore.value = data.paging?.hasNext ?? false;
      } else {
        hasMore.value = false;
      }
    } finally {
      loadingMore.value = false;
    }
  }

  return {
    query,
    scope,
    sort,
    results,
    nextCursor,
    hasMore,
    loading,
    loadingMore,
    error,
    hasSearched,
    canSearch,
    reset,
    setScope,
    setSort,
    search,
    loadMore,
  };
});
