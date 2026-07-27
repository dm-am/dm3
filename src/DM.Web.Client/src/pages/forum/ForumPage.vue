<script setup lang="ts">
/**
 * ForumPage — persistent shell for every forum route (/forum,
 * /forum/:alias, /forum/:alias/:num). It renders the shared header stack
 * (h1 "Форум" + board strip) exactly once: because all forum routes are
 * children of the same route record, this component is never remounted
 * between forum pages, so BoardNavigation keeps a single live instance
 * and its active-first FLIP animation is actually visible when the
 * reader switches boards.
 */
import { computed, provide, ref } from "vue";
import { useRoute } from "vue-router";
import { useBoardsStore } from "@/entities/forum";
import { storeToRefs } from "pinia";
import { useFetchData } from "@/shared/lib/composables/useFetchData";
import { ErrorPage, getErrorConfig } from "@/shared/ui/ErrorPage";
import { useDocumentTitle } from "@/shared/lib/composables/useDocumentTitle";
import BoardNavigation from "./BoardNavigation.vue";
import { reportForumShellError } from "./forumShell";

const route = useRoute();
const boardsStore = useBoardsStore();
const { selectedBoard, topics, attachedTopics } = storeToRefs(boardsStore);
const { trySelectBoardByAlias, fetchBoards } = boardsStore;

// The /forum index navigates with its boards table — the strip would
// duplicate it, so the shell only shows the strip on board/topic levels.
const boardAlias = computed(() => route.params.alias as string | undefined);

// Error code when the requested board alias fails to resolve: 404 for a
// genuinely missing board, 500 for anything else (network/server failure).
// Distinguishes a missing board from a board that simply has no topics
// (TopicsList shows empty state).
const errorCode = ref<number | null>(null);

// Leaf-reported page-level failure (topic not found, …): the shell hides
// its header stack and renders the full-screen ErrorPage itself, so an
// error is never shown squeezed under the forum title and board strip.
const leafErrorCode = ref<number | null>(null);
provide(reportForumShellError, (code) => {
  leafErrorCode.value = code;
});

const shownErrorCode = computed(() => errorCode.value ?? leafErrorCode.value);

// ErrorPage itself does not set document.title (only the route-level
// ErrorPageRoute.vue does) — this shell owns the title while the embedded
// ErrorPage is showing, and on the /forum index (no dynamic entity there).
// On the happy path of board/topic routes the leaf view (TopicsList /
// TopicPage) owns the title once its data loads.
useDocumentTitle(() => {
  if (shownErrorCode.value) return getErrorConfig(shownErrorCode.value).title;
  return boardAlias.value ? null : "Форум";
});

/** Maps a failed board lookup's HTTP status to an ErrorPage code. */
function mapErrorCode(status: number | undefined): number {
  if (status === 404) return 404;
  return 500;
}

async function fetchData() {
  const alias = route.params.alias as string | undefined;
  errorCode.value = null;
  // Drop the previously selected board so a failed lookup can't leave a stale
  // board (and its topics) visible under a wrong URL. The old board's topics
  // are cleared too: cross-board navigation is a different dataset, so the
  // table shows its skeleton instead of the previous board's rows
  // (stale-while-revalidate is for same-board refreshes only).
  selectedBoard.value = null;
  topics.value = null;
  attachedTopics.value = null;
  // The /forum index has no board to resolve — its leaf (ForumIndexPage)
  // loads the boards table itself.
  if (!alias) return;
  // Load boards for navigation and select current board
  await fetchBoards();
  const { ok, status } = await trySelectBoardByAlias(alias);
  // Stale continuation: the user navigated to another board while this one
  // was resolving — that navigation's own fetchData owns all state now.
  if ((route.params.alias as string | undefined) !== alias) return;
  if (!ok) {
    errorCode.value = mapErrorCode(status);
    return;
  }
  // Topics are loaded by TopicsList.vue via paramsKey watcher
}

useFetchData(
  () => fetchData(),
  [
    {
      param: (p) => p.alias,
      callback: () => fetchData(),
    },
  ],
  // No query watchers - pagination handled by TopicsList via URL
);
</script>

<template>
  <ErrorPage v-if="shownErrorCode" :code="shownErrorCode" />
  <!-- v-show (not v-else/v-if): the leaf inside <router-view> must stay
       mounted while it reports an error, otherwise its unmount would clear
       the very error it reported. -->
  <div v-show="!shownErrorCode">
    <!-- Shared forum header stack, owned by the shell so it persists across
         all forum levels. On board/topic pages the strip's highlighted item
         names the current board and carries the whole "where am I" role. -->
    <page-title v-once>Форум</page-title>
    <BoardNavigation v-if="boardAlias" />
    <router-view />
  </div>
</template>
