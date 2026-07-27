<template>
  <SidebarEntityList
    token="FinishedGames"
    title="Завершенные игры"
    :lines="5"
    :items="store.finishedGames"
    :errored="failed"
    empty="Завершенных игр пока нет"
    :retry="() => fetchFinishedGames(true)"
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
import { useUserStore } from "@/entities/user";
import { onMounted, ref, watch } from "vue";
import { useRoute } from "vue-router";

const store = useGamesStore();
const userStore = useUserStore();
const route = useRoute();

// The games store does not expose an error ref for this list, so detect
// failure locally: when a fetch settles and the list is still null, the
// request failed (prevents an eternal skeleton).
const failed = ref(false);

async function fetchFinishedGames(force = false) {
  await store.fetchFinishedGames(force);
  failed.value = store.finishedGames === null;
}

onMounted(() => fetchFinishedGames());

// Refetch only on actual login/logout to keep unread counters accurate
// (force=true because a plain fetch() no-ops inside the cache TTL).
watch(
  () => userStore.user?.username,
  (newUsername, oldUsername) => {
    if ((newUsername && !oldUsername) || (!newUsername && oldUsername)) {
      fetchFinishedGames(true);
    }
  },
);

// Re-trigger on navigation so a failed fetch gets another chance once the
// TTL cache considers it stale.
watch(
  () => route.fullPath,
  () => fetchFinishedGames(),
);
</script>
