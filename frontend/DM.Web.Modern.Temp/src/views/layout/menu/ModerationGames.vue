<template>
  <menu-block token="ModerationGames">
    <template #title>Требуют премодерации</template>
    <the-loader v-if="!store.moderationGames" />
    <template v-else-if="store.moderationGames.length === 0">
      <secondary-text>Пока тут ничего нет...</secondary-text>
    </template>
    <game-menu-link
      v-else
      v-for="game in store.moderationGames"
      :key="game.id"
      :game="game"
      :counters="true"
      :always-show-counters="true"
    />
    <div class="separator">- - - - - - - - - - - - - - - - - - - - - - - - - -</div>
    <div>
      <span class="muted">-</span> <router-link class="forward" :to="{ name: 'games', params: { status: 'moderation' } }">Все премодерируемые игры</router-link>
    </div>
  </menu-block>
</template>

<script setup lang="ts">
import MenuBlock from "@/views/layout/MenuBlock.vue";
import TheLoader from "@/components/TheLoader.vue";
import SecondaryText from "@/components/layout/SecondaryText.vue";
import GameMenuLink from "@/views/layout/menu/GameMenuLink.vue";
import { useGamesStore } from "@/stores/games";
import { onMounted } from "vue";

const store = useGamesStore();

onMounted(() => store.fetchModerationGames());
</script>

<style scoped lang="sass">
@import "src/assets/styles/Themes"

.forward
  font-weight: bold

.muted
  +theme(color, $secondary-text)

.separator
  +theme(color, $secondary-text)
</style>
