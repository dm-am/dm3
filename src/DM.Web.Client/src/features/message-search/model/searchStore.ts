import { defineStore } from "pinia";
import { computed, ref } from "vue";
import { createRequestGuard } from "@/shared/lib/utils/requestGuard";
import type { MessageSearchResult, MessageSearchScope } from "./types";
import messageSearchApi from "../api/messageSearchApi";

const PAGE_SIZE = 50;

// Not part of the live chat store: hits are a list with their own cursor; the order is the server's only one.
export const useMessageSearchStore = defineStore("messageSearch", () => {
  const query = ref("");
  /** The hits of one feed are no answer in another. */
  const scope = ref<MessageSearchScope>("global");

  const results = ref<MessageSearchResult[]>([]);
  const nextCursor = ref<string | null>(null);
  const hasMore = ref(false);

  const loading = ref(false);
  const loadingMore = ref(false);
  const error = ref<string | null>(null);
  /** Tells the initial prompt from a genuine "no results". */
  const hasSearched = ref(false);

  const trimmedQuery = computed(() => query.value.trim());

  const canSearch = computed(() => trimmedQuery.value.length > 0);

  // One counter for search and loadMore: two requests race, and only the newest may land.
  const guard = createRequestGuard();

  function reset() {
    // An answer still on the wire must not repopulate what this just cleared.
    guard.next();
    results.value = [];
    nextCursor.value = null;
    hasMore.value = false;
    error.value = null;
    hasSearched.value = false;
    loading.value = false;
    loadingMore.value = false;
  }

  // The store is one for the site: hits kept over the next feed would jump to messages it lacks.
  function setScope(next: MessageSearchScope) {
    if (next === scope.value) return;
    scope.value = next;
    query.value = "";
    reset();
  }

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
        in: [scope.value],
        limit: PAGE_SIZE,
      });
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
      // Only the newest request may lower the spinner.
      if (guard.isCurrent(requestId)) loading.value = false;
    }
  }

  async function loadMore() {
    if (loadingMore.value || !hasMore.value || !nextCursor.value) return;
    const requestId = guard.next();
    loadingMore.value = true;
    error.value = null;
    try {
      const { data, error: apiError } = await messageSearchApi.searchMessages({
        search: trimmedQuery.value,
        in: [scope.value],
        cursor: nextCursor.value,
        limit: PAGE_SIZE,
      });
      if (!guard.isCurrent(requestId)) return;
      if (apiError) {
        // Keep the results and the cursor so the sentinel's retry can try again.
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
    search,
    loadMore,
  };
});
