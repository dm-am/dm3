<script setup lang="ts">
import { ref } from "vue";
import { useRoute } from "vue-router";
import { useBoardsStore } from "@/entities/forum";
import { storeToRefs } from "pinia";
import { useFetchData } from "@/shared/lib/composables/useFetchData";
import { ErrorPage } from "@/shared/ui/ErrorPage";

const route = useRoute();
const boardsStore = useBoardsStore();
const { selectedBoard } = storeToRefs(boardsStore);
const { trySelectBoardByAlias, fetchBoards } = boardsStore;

// 404 when the requested board alias does not resolve. Distinguishes a missing
// board from a board that simply has no topics (TopicsList shows empty state).
// The document title is owned by the leaf views (TopicsList / TopicPage), not
// this layout parent, to avoid a parent/child title race.
const notFound = ref(false);

async function fetchData() {
  const alias = route.params.alias as string;
  notFound.value = false;
  // Drop the previously selected board so a failed lookup can't leave a stale
  // board (and its topics) visible under a wrong URL.
  selectedBoard.value = null;
  // Load boards for navigation and select current board
  await fetchBoards();
  const selected = await trySelectBoardByAlias(alias);
  if (!selected) {
    notFound.value = true;
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
  <ErrorPage v-if="notFound" :code="404" />
  <template v-else>
    <page-title>Форум</page-title>
    <!-- All content (navigation, moderators, filters, table) rendered by TopicsList -->
    <router-view />
  </template>
</template>
