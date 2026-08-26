<template>
  <SidebarEntityList
    token="PopularGames"
    title="Популярные игры"
    :lines="10"
    :items="store.popularGames"
    :errored="!!store.popularGamesError"
    empty="Популярных игр пока нет"
    :retry="() => store.fetchPopularGames(true)"
    :forward-to="{
      name: 'games',
      query: { sortBy: 'popularity', sortOrder: 'desc' },
    }"
    forward-label="Все популярные игры"
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

useSidebarRefresh(store.fetchPopularGames);
</script>
