<template>
  <SidebarEntityList
    token="RecruitingGames"
    title="Набор игроков"
    :lines="15"
    :items="store.recruitingGames"
    :errored="failed"
    empty="Игр с набором пока нет"
    :retry="() => fetchRecruitingGames(true)"
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

async function fetchRecruitingGames(force = false) {
  await store.fetchRecruitingGames(force);
  failed.value = store.recruitingGames === null;
}

onMounted(() => fetchRecruitingGames());

// Refetch only on actual login/logout to keep unread counters accurate
// (force=true because a plain fetch() no-ops inside the cache TTL).
watch(
  () => userStore.user?.username,
  (newUsername, oldUsername) => {
    if ((newUsername && !oldUsername) || (!newUsername && oldUsername)) {
      fetchRecruitingGames(true);
    }
  },
);

// Re-trigger on navigation so a failed fetch gets another chance once the
// TTL cache considers it stale.
watch(
  () => route.fullPath,
  () => fetchRecruitingGames(),
);
</script>
