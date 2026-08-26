<template>
  <SidebarEntityList
    token="RecruitingGames"
    title="Набор игроков"
    :lines="15"
    :items="store.recruitingGames"
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
      <SidebarGameLink
        :game="item"
        :counters="true"
        :always-show-counters="!userStore.user"
      />
    </template>
  </SidebarEntityList>
</template>

<script setup lang="ts">
import SidebarEntityList from "./SidebarEntityList.vue";
import SidebarGameLink from "./SidebarGameLink.vue";
import { useGamesStore } from "@/entities/game";
import { useAuthStore } from "@/entities/user";
import { useSidebarRefresh } from "./useSidebarRefresh";

const store = useGamesStore();
const userStore = useAuthStore();

useSidebarRefresh(store.fetchRecruitingGames);
</script>
