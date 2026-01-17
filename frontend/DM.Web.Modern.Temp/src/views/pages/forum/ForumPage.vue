<script setup lang="ts">
import { useRoute } from "vue-router";
import { useBoardsStore } from "@/stores";
import { storeToRefs } from "pinia";
import { extractNumberParam } from "@/router";
import type { BoardId } from "@/api/models/forum";
import { useFetchData } from "@/composables/useFetchData";

const route = useRoute();
const boardsStore = useBoardsStore();
const { moderators } = storeToRefs(boardsStore);
const { trySelectBoard, fetchModerators, fetchTopics } = boardsStore;

async function fetchData() {
  const boardId = route.params.id as BoardId;
  await trySelectBoard(boardId);

  await Promise.all([
    fetchModerators(),
    fetchTopics(extractNumberParam(route.params.n)),
  ]);
}

useFetchData(
  () => fetchData(),
  [
    {
      param: (p) => p.id,
      callback: () => fetchData(),
    },
    {
      param: (p) => p.n,
      callback: (n) => fetchTopics(extractNumberParam(n)),
    },
  ],
);
</script>

<template>
  <page-title>Форум | {{ route.params.id }}</page-title>

  <div class="forum-info">
    <div class="forum-info_moderators">
      <block-title class="forum-info_moderators-title">Модераторы:</block-title>
      <the-loader v-if="!moderators" class="forum-info_moderators-loader" />
      <user-link
        v-else
        v-for="user in moderators"
        :key="user.login"
        :user="user"
      />
    </div>
  </div>

  <router-view />
</template>

<style scoped lang="sass">
@import "src/assets/styles/Variables"

.forum-info
  margin: $medium 0

.forum-info_moderators-title
  display: inline-block
  margin: 0 $medium 0 0

.forum-info_moderators-loader
  display: inline-block
</style>
