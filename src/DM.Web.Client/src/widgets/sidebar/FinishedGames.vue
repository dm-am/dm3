<template>
  <SidebarBlock token="FinishedGames">
    <template #title>Завершенные игры</template>
    <SidebarSkeleton v-if="store.finishedGames === null" :lines="5" />
    <SecondaryText v-else-if="store.finishedGames.length === 0">
      Нет завершенных игр
    </SecondaryText>
    <GameLink
      v-else
      v-for="game in store.finishedGames"
      :key="game.id"
      :game="game"
      :counters="true"
      :always-show-counters="!userStore.user"
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
          query: { status: 'Closed', closedReasonFilter: 'Finished', sortBy: 'closed' },
        }"
        >Все завершенные игры</router-link
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

onMounted(() => store.fetchFinishedGames());
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
