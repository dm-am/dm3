<template>
  <SidebarEntityList
    token="RecruitingGames"
    title="Набор игроков"
    :lines="15"
    :items="recruiting"
    :errored="!!store.recruitingGamesError"
    empty="Игр с набором пока нет"
    :retry="() => store.fetchRecruitingGames(true)"
    :forward-to="{
      name: 'games',
      query: {
        status: 'Active',
        recruitmentFilter: 'open',
        sortBy: 'activated',
      },
    }"
    forward-label="Все игры с набором"
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
const { recruiting, aboveRecruiting } = useLeftSidebarGames();
useFilledRows({
  items: () => store.recruitingGames,
  rows: () => recruiting.value,
  above: () => aboveRecruiting.value,
  limit: SIDEBAR_GAME_ROWS.recruiting,
  take: () => store.recruitingGamesTake,
  widen: store.widenRecruitingGames,
});

useSidebarRefresh(store.fetchRecruitingGames);
</script>
