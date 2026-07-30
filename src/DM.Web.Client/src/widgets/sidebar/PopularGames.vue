<template>
  <SidebarBlock token="PopularGames">
    <template #title>Популярные игры</template>
    <SidebarSkeleton
      v-if="store.popularGames === null && !failed"
      :lines="10"
    />
    <SecondaryText v-else-if="store.popularGames === null">
      Не удалось загрузить.
      <button type="button" class="retry-link" @click="fetchPopularGames(true)">
        Повторить
      </button>
    </SecondaryText>
    <SecondaryText v-else-if="store.popularGames.length === 0">
      Популярных игр пока нет
    </SecondaryText>
    <template v-else>
      <SidebarGameLink
        v-for="game in store.popularGames"
        :key="game.id"
        :game="game"
        :counters="true"
        :alwaysShowCounters="!userStore.user"
      />
    </template>
    <DashSeparator spacing="tiny" width="75%" />
    <div>
      <span class="muted">- </span>
      <router-link
        class="forward"
        :to="{
          name: 'games',
          query: { sortBy: 'popularity', sortOrder: 'desc' },
        }"
        >Все популярные игры</router-link
      >
    </div>
  </SidebarBlock>
</template>

<script setup lang="ts">
import SidebarBlock from "./SidebarBlock.vue";
import SidebarSkeleton from "./SidebarSkeleton.vue";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";
import SidebarGameLink from "./SidebarGameLink.vue";
import { useGamesStore } from "@/entities/game";
import { useAuthStore } from "@/entities/user";
import { onMounted, ref } from "vue";
import { DashSeparator } from "@/shared/ui/DashSeparator";

const store = useGamesStore();
const userStore = useAuthStore();

// The games store does not expose an error ref for this list, so detect
// failure locally: when the fetch settles and the list is still null,
// the request failed (prevents an eternal skeleton).
const failed = ref(false);

async function fetchPopularGames(force = false) {
  await store.fetchPopularGames(force);
  failed.value = store.popularGames === null;
}

onMounted(() => fetchPopularGames());
</script>

<style scoped lang="sass">
@import "@/assets/styles/Inputs"

.muted
  color: $text-muted

.forward
  font-weight: bold

.retry-link
  +inline-link-button
</style>
