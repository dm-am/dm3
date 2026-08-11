<template>
  <SidebarBlock token="PopularGames">
    <template #title>Популярные игры</template>
    <SidebarSkeleton
      v-if="store.popularGames === null && !store.popularGamesError"
      :lines="10"
    />
    <SecondaryText v-else-if="store.popularGames === null">
      Не удалось загрузить.
      <button
        type="button"
        class="retry-link"
        @click="store.fetchPopularGames(true)"
      >
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
import { onMounted } from "vue";
import { DashSeparator } from "@/shared/ui/DashSeparator";

const store = useGamesStore();
const userStore = useAuthStore();

onMounted(() => store.fetchPopularGames());
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
