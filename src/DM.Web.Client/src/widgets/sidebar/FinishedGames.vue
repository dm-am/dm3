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
import { onMounted, watch } from "vue";
import { useRoute } from "vue-router";
import { useViewerChange } from "@/shared/lib/composables";

const store = useGamesStore();
const userStore = useAuthStore();
const route = useRoute();

onMounted(() => store.fetchFinishedGames());

// Refetch on any change of viewer to keep unread counters accurate (force=true
// because a plain fetch() no-ops inside the cache TTL). See useViewerChange for
// why "logged in or out" was the wrong question.
useViewerChange(() => store.fetchFinishedGames(true));

// Re-trigger on navigation so a failed fetch gets another chance once the
// TTL cache considers it stale.
watch(
  () => route.fullPath,
  () => store.fetchFinishedGames(),
);
</script>
