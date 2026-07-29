<template>
  <SidebarEntityList
    v-if="!userStore.user"
    token="ActiveGames"
    title="Активные игры"
    :lines="5"
    :items="store.activeGames"
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
      <SidebarGameLink
        :game="item"
        :counters="true"
        :always-show-counters="true"
      />
    </template>
  </SidebarEntityList>
</template>

<script setup lang="ts">
import SidebarEntityList from "./SidebarEntityList.vue";
import SidebarGameLink from "./SidebarGameLink.vue";
import { useGamesStore } from "@/entities/game";
import { useAuthStore } from "@/entities/user";
import { onMounted, watch } from "vue";
import { useRoute } from "vue-router";

const store = useGamesStore();
const userStore = useAuthStore();
const route = useRoute();

onMounted(() => {
  if (!userStore.user) {
    store.fetchActiveGames();
  }
});

watch(
  () => userStore.user,
  (user) => {
    if (!user) {
      store.fetchActiveGames();
    }
  },
);

// Re-trigger on navigation so a failed fetch gets another chance once the
// TTL cache considers it stale. Guarded the same way as the mount fetch —
// this block only renders (and fetches) for guests.
watch(
  () => route.fullPath,
  () => {
    if (!userStore.user) {
      store.fetchActiveGames();
    }
  },
);
</script>
