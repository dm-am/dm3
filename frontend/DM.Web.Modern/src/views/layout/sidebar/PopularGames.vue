<template>
  <menu-block token="PopularGames">
    <template #title>Популярные игры</template>
    <the-loader v-if="!store.popularGames" />
    <template v-else-if="store.popularGames.length === 0">
      <secondary-text>Пока тут ничего нет...</secondary-text>
    </template>
    <template v-else>
      <game-menu-link
        v-for="game in store.popularGames"
        :key="game.id"
        :game="game"
        :counters="true"
      />
    </template>
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

onMounted(() => store.fetchPopularGames());
</script>
