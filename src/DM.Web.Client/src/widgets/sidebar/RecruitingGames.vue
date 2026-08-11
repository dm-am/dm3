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
import { onMounted, watch } from "vue";
import { useRoute } from "vue-router";
import { useViewerChange } from "@/shared/lib/composables";

const store = useGamesStore();
const userStore = useAuthStore();
const route = useRoute();

onMounted(() => store.fetchRecruitingGames());

// Refetch on any change of viewer to keep unread counters accurate (force=true
// because a plain fetch() no-ops inside the cache TTL).
useViewerChange(() => store.fetchRecruitingGames(true));

// Re-trigger on navigation so a failed fetch gets another chance once the
// TTL cache considers it stale.
watch(
  () => route.fullPath,
  () => store.fetchRecruitingGames(),
);
</script>
