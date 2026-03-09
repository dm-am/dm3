<template>
  <menu-block token="ModerationGames">
    <template #title>Требуют премодерации</template>
    <template v-if="!store.moderationGames || store.moderationGames.length === 0">
      <secondary-text>У вас нет курируемых игр</secondary-text>
    </template>
    <game-menu-link
      v-else
      v-for="game in store.moderationGames"
      :key="game.id"
      :game="game"
      :counters="true"
      :always-show-counters="true"
    />
    <div class="separator">
      - - - - - - - - - - - - - - - - - - - - - - - - - -
    </div>
    <div>
      <span class="muted">- </span
      ><router-link
        class="forward"
        :to="{ name: 'games-moderation' }"
        >Все премодерируемые игры</router-link
      >
    </div>
  </menu-block>
</template>

<script setup lang="ts">
import MenuBlock from "./MenuBlock.vue";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";
import GameMenuLink from "./GameMenuLink.vue";
import { useGamesStore } from "@/entities/game";
import { onMounted } from "vue";

const store = useGamesStore();

onMounted(() => store.fetchModerationGames());
</script>

<style scoped lang="sass">
@import "src/assets/styles/Themes"

.forward
  font-weight: bold

.muted
  color: $text-muted

.separator
  color: $text-muted
</style>
