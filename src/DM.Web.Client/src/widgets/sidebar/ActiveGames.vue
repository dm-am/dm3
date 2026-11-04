<template>
  <SidebarEntityList
    v-if="!userStore.user"
    token="ActiveGames"
    title="Активные игры"
    :lines="5"
    :items="active"
    :errored="!!store.activeGamesError"
    empty="Активных игр пока нет"
    :retry="() => store.fetchActiveGames(true)"
    :forward-to="{
      name: 'games',
      query: {
        status: 'Active',
        recruitmentFilter: 'closed',
        sortBy: 'activated',
      },
    }"
    forward-label="Все активные игры"
  >
    <template #item="{ item }">
      <SidebarGameLink :game="item" :always-show-counters="true" />
    </template>
  </SidebarEntityList>
</template>

<script setup lang="ts">
import SidebarEntityList from "./SidebarEntityList.vue";
import SidebarGameLink from "./SidebarGameLink.vue";
import { SIDEBAR_GAME_ROWS, useGamesStore } from "@/entities/game";
import { useAuthStore } from "@/entities/user";
import { onMounted, watch } from "vue";
import { useRoute } from "vue-router";
import { useFilledRows, useLeftSidebarGames } from "./leftSidebarRows";

const store = useGamesStore();
const userStore = useAuthStore();
const route = useRoute();

// Without the games a block above shows; for a viewer, who has no such block,
// the list asks the server for no more rows.
const { active, aboveActive } = useLeftSidebarGames();
useFilledRows({
  items: () => (userStore.user ? null : store.activeGames),
  rows: () => active.value,
  above: () => aboveActive.value,
  limit: SIDEBAR_GAME_ROWS.active,
  take: () => store.activeGamesTake,
  widen: store.widenActiveGames,
});

// The block is the guest's alone.
function fetchForGuest() {
  if (!userStore.user) store.fetchActiveGames();
}

onMounted(fetchForGuest);
watch(() => userStore.user, fetchForGuest);
// On navigation too, so a failed fetch gets another chance once the cache is stale.
watch(() => route.fullPath, fetchForGuest);
</script>
