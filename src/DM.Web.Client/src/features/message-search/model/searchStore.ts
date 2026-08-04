import { defineStore } from "pinia";
import { computed, ref } from "vue";
import { createRequestGuard } from "@/shared/lib/utils/requestGuard";
import type { MessageSearchResult } from "./types";
import messageSearchApi from "../api/messageSearchApi";

const PAGE_SIZE = 50;

/**
 * The only scope this search ever asks for.
 *
 * The endpoint can search every source the reader may read, but the one entry
 * point into it is the field above the global chat, and a field above the
 * global chat searches the global chat. Anything wider needs a page of its own
 * before it needs a control.
 */
const GLOBAL_SCOPE = ["global"];

/**
 * Dedicated search store. Deliberately NOT part of the live global-chat store:
 * search results are a distinct navigable list with its own cursor, not the
 * live scroll-back buffer.
 *
 * Order is the backend's and there is no way to ask for another one: relevance
 * ranking exists neither in the domain nor in the endpoint's signature, so a
 * control offering it would only be a promise the site does not keep.
 */
export const useMessageSearchStore = defineStore("messageSearch", () => {
  // Query
  const query = ref("");

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

  /** Whether a search can currently run. */
  const canSearch = computed(() => trimmedQuery.value.length > 0);

  /**
   * One counter for both requests, not one each.
   *
   * The field is debounced by 300ms and a search does not cancel the one before
   * it, so two are regularly on the wire at once: the slower answer landed last
   * and put the hits for "коб" under the text "кобольд", with nothing on screen
   * saying they disagreed. loadMore shares the counter because a page that
   * started under the older query would otherwise be appended to the newer
   * query's results and would move its cursor.
   */
  const guard = createRequestGuard();

  function reset() {
    // An answer still on the wire must not repopulate what this just cleared:
    // the field going empty is the caller.
    guard.next();
    results.value = [];
    nextCursor.value = null;
    hasMore.value = false;
    error.value = null;
    hasSearched.value = false;
    loading.value = false;
    loadingMore.value = false;
  }

  /** Run a fresh search from the top (replaces results). */
  async function search() {
    if (!canSearch.value) {
      reset();
      return;
    }
    const requestId = guard.next();
    loading.value = true;
    error.value = null;
    hasSearched.value = true;
    try {
      const { data, error: apiError } = await messageSearchApi.searchMessages({
        search: trimmedQuery.value,
        in: GLOBAL_SCOPE,
        limit: PAGE_SIZE,
      });
      // A newer search owns the visible list.
      if (!guard.isCurrent(requestId)) return;
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
      // Only the newest request may lower the spinner: an older one clearing it
      // presents the request still on the wire as finished.
      if (guard.isCurrent(requestId)) loading.value = false;
    }
  }

  /** Load the next (older) page and append it to the results. */
  async function loadMore() {
    if (loadingMore.value || !hasMore.value || !nextCursor.value) return;
    const requestId = guard.next();
    loadingMore.value = true;
    error.value = null;
    try {
      const { data, error: apiError } = await messageSearchApi.searchMessages({
        search: trimmedQuery.value,
        in: GLOBAL_SCOPE,
        cursor: nextCursor.value,
        limit: PAGE_SIZE,
      });
      // A search started after this page did: its results are the list now, and
      // appending an older query's tail to them would also move its cursor.
      if (!guard.isCurrent(requestId)) return;
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
    results,
    nextCursor,
    hasMore,
    loading,
    loadingMore,
    error,
    hasSearched,
    canSearch,
    reset,
    search,
    loadMore,
  };
});
