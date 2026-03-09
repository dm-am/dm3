<script setup lang="ts">
import { useRoute } from "vue-router";
import { useBoardsStore } from "@/entities/forum";
import { storeToRefs } from "pinia";
import { extractNumberParam } from "@/app/providers/router";
import type { BoardId } from "@/entities/forum";
import { useFetchData } from "@/shared/lib/composables/useFetchData";

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
      <secondary-text v-if="moderators && !moderators.length">Нет модераторов</secondary-text>
      <user-link
        v-else
        v-for="user in moderators"
        :key="user.username"
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
