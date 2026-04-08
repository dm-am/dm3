<template>
  <SidebarBlock token="RecruitingGames">
    <template #title>Набор игроков</template>
    <SidebarSkeleton v-if="store.recruitingGames === null" :lines="15" />
    <SecondaryText v-else-if="store.recruitingGames.length === 0">
      Нет игр с набором
    </SecondaryText>
    <GameLink
      v-else
      v-for="game in store.recruitingGames"
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
          query: { status: 'Active', recruitmentFilter: 'open', sortBy: 'activated' },
        }"
        >Все игры с набором</router-link
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

onMounted(() => store.fetchRecruitingGames());
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
