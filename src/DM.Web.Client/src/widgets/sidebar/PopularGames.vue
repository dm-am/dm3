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
import { onMounted, watch } from "vue";
import { useRoute } from "vue-router";
import { useViewerChange } from "@/shared/lib/composables/useViewerChange";

const store = useGamesStore();
const userStore = useAuthStore();
const route = useRoute();

onMounted(() => store.fetchPopularGames());

// Refetch on any change of viewer to keep unread counters accurate (force=true
// because a plain fetch() no-ops inside the cache TTL). RightSidebar mounts
// this block once and never unmounts it, so onMounted alone fired once per
// page load: a second tab signing another account in left the previous
// viewer's counters on the rows for the rest of the session.
useViewerChange(() => store.fetchPopularGames(true));

// Re-trigger on navigation so a failed fetch gets another chance once the
// TTL cache considers it stale.
watch(
  () => route.fullPath,
  () => store.fetchPopularGames(),
);
</script>
