<template>
  <SidebarEntityList
    token="FinishedGames"
    title="Завершенные игры"
    :lines="5"
    :items="finished"
    :errored="!!store.finishedGamesError"
    empty="Завершенных игр пока нет"
    :retry="() => store.fetchFinishedGames(true)"
    :forward-to="{
      name: 'games',
      query: {
        status: 'Closed',
        closedReasonFilter: 'Finished',
        sortBy: 'closed',
      },
    }"
    forward-label="Все завершенные игры"
  >
    <template #item="{ item }">
      <SidebarGameLink :game="item" :always-show-counters="!userStore.user" />
    </template>
  </SidebarEntityList>
</template>

<script setup lang="ts">
import SidebarEntityList from "./SidebarEntityList.vue";
import SidebarGameLink from "./SidebarGameLink.vue";
import { SIDEBAR_GAME_ROWS, useGamesStore } from "@/entities/game";
import { useAuthStore } from "@/entities/user";
import { useSidebarRefresh } from "./useSidebarRefresh";
import { useFilledRows, useLeftSidebarGames } from "./leftSidebarRows";

const store = useGamesStore();
const userStore = useAuthStore();

// Without the games a block above shows.
const { finished, aboveFinished } = useLeftSidebarGames();
useFilledRows({
  items: () => store.finishedGames,
  rows: () => finished.value,
  above: () => aboveFinished.value,
  limit: SIDEBAR_GAME_ROWS.finished,
  take: () => store.finishedGamesTake,
  widen: store.widenFinishedGames,
});

useSidebarRefresh(store.fetchFinishedGames);
</script>
