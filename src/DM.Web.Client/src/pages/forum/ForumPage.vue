<script setup lang="ts">
import { useRoute } from "vue-router";
import { useBoardsStore } from "@/entities/forum";
import { storeToRefs } from "pinia";
import { useFetchData } from "@/shared/lib/composables/useFetchData";

const route = useRoute();
const boardsStore = useBoardsStore();
const { selectedBoard } = storeToRefs(boardsStore);
const { trySelectBoardByAlias, fetchModerators, fetchBoards } = boardsStore;

async function fetchData() {
  const alias = route.params.alias as string;
  // Load boards for navigation and select current board
  await fetchBoards();
  await trySelectBoardByAlias(alias);
  await fetchModerators();
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
  <page-title>Форум</page-title>
  <!-- All content (navigation, moderators, filters, table) rendered by TopicsList -->
  <router-view />
</template>
