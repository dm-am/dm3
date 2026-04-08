<template>
  <SidebarBlock token="PopularGames">
    <template #title>Популярные игры</template>
    <SidebarSkeleton v-if="store.popularGames === null" :lines="10" />
    <SecondaryText v-else-if="store.popularGames.length === 0">
      Нет популярных игр
    </SecondaryText>
    <template v-else>
      <GameLink
        v-for="game in store.popularGames"
        :key="game.id"
        :game="game"
        :counters="true"
        :alwaysShowCounters="!userStore.user"
      />
    </template>
    <div class="separator">
      - - - - - - - - - - - - - - - - - - - - - - - - - -
    </div>
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
import GameLink from "./GameLink.vue";
import { useGamesStore } from "@/entities/game";
import { useUserStore } from "@/entities/user";
import { onMounted } from "vue";

const store = useGamesStore();
const userStore = useUserStore();

onMounted(() => store.fetchPopularGames());
</script>

<style scoped lang="sass">
@import "src/assets/styles/Themes"

.muted
  color: $text-muted

.forward
  font-weight: bold

.separator
  color: $text-muted
</style>
