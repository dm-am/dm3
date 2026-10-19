<script setup lang="ts">
/**
 * The shell of every forum route: all of them are children of one route
 * record, so the header and the board strip are drawn once and the strip's
 * FLIP animation is seen when the reader switches boards.
 */
import { computed, provide, ref } from "vue";
import { useRoute } from "vue-router";
import { useBoardsStore } from "@/entities/forum";
import { OWNS_REFUSAL } from "@/shared/api/interceptor";
import { storeToRefs } from "pinia";
import { useFetchData } from "@/shared/lib/composables/useFetchData";
import {
  ErrorPage,
  errorCodeForStatus,
  getErrorConfig,
} from "@/shared/ui/ErrorPage";
import { useDocumentTitle } from "@/shared/lib/composables/useDocumentTitle";
import BoardNavigation from "./BoardNavigation.vue";
import { reportForumShellError } from "./forumShell";

const route = useRoute();
const boardsStore = useBoardsStore();
const { selectedBoard, topics, attachedTopics } = storeToRefs(boardsStore);
const { trySelectBoardByAlias, fetchBoards } = boardsStore;

// The index navigates with its boards table, so the strip stands on board and topic levels only.
const boardAlias = computed(() => route.params.alias as string | undefined);

// A board that did not resolve, told from one with no topics (errorCodeForStatus).
const errorCode = ref<number | null>(null);

// A leaf's page-level failure: the shell draws the full ErrorPage, never under the header.
const leafErrorCode = ref<number | null>(null);
provide(reportForumShellError, (code) => {
  leafErrorCode.value = code;
});

const shownErrorCode = computed(() => errorCode.value ?? leafErrorCode.value);

// The shell owns the title while its ErrorPage shows and on the index; the
// leaf owns it on board and topic pages once its data loads.
useDocumentTitle(() => {
  if (shownErrorCode.value) return getErrorConfig(shownErrorCode.value).title;
  return boardAlias.value ? null : "Форум";
});

async function fetchData() {
  const alias = route.params.alias as string | undefined;
  errorCode.value = null;
  // A failed lookup must not leave the previous board and its topics under a
  // wrong URL, so both are dropped: another board shows its skeleton.
  selectedBoard.value = null;
  topics.value = null;
  attachedTopics.value = null;
  // The index loads its boards table itself.
  if (!alias) return;
  // The read owns its refusal: the error page of the status stands in place of the board.
  await fetchBoards();
  const { ok, status } = await trySelectBoardByAlias(alias, OWNS_REFUSAL);
  // Another board was asked for meanwhile; its own fetch owns the state now.
  if ((route.params.alias as string | undefined) !== alias) return;
  if (!ok) {
    errorCode.value = errorCodeForStatus(status);
    return;
  }
}

useFetchData(
  () => fetchData(),
  [
    {
      param: (p) => p.alias,
      callback: () => fetchData(),
    },
  ],
);
</script>

<template>
  <ErrorPage v-if="shownErrorCode" :code="shownErrorCode" />
  <!-- v-show: the leaf must stay mounted while it reports an error, or its unmount clears it. -->
  <div v-show="!shownErrorCode">
    <!-- On board and topic pages the strip's highlighted item names the board. -->
    <page-title v-once>Форум</page-title>
    <BoardNavigation v-if="boardAlias" />
    <router-view />
  </div>
</template>
