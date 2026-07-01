<template>
  <SidebarBlock v-if="!userStore.user" token="ActiveGames">
    <template #title>Активные игры</template>
    <SidebarSkeleton
      v-if="store.activeGames === null && !store.activeGamesError"
      :lines="5"
    />
    <SecondaryText v-else-if="store.activeGames === null">
      Не удалось загрузить
    </SecondaryText>
    <SecondaryText v-else-if="store.activeGames.length === 0">
      Активных игр пока нет
    </SecondaryText>
    <GameLink
      v-else
      v-for="game in store.activeGames"
      :key="game.id"
      :game="game"
      :counters="true"
      :always-show-counters="true"
    />
    <div class="separator">
      - - - - - - - - - - - - - - - - - - - - - - - - - -
    </div>
    <div>
      <span class="muted">- </span>
      <router-link
        class="forward"
        :to="{
          name: 'games',
          query: {
            status: 'Active',
            recruitmentFilter: 'closed',
            sortBy: 'activated',
          },
        }"
        >Все активные игры</router-link
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
import { onMounted, watch } from "vue";

const store = useGamesStore();
const userStore = useUserStore();

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
