<template>
  <SidebarEntityList
    token="FinishedGames"
    title="Завершенные игры"
    :lines="5"
    :items="store.finishedGames"
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

useSidebarRefresh(store.fetchFinishedGames);
</script>
