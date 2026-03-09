<template>
  <menu-block token="PopularGames">
    <template #title>Популярные игры</template>
    <template v-if="!store.popularGames || store.popularGames.length === 0">
      <secondary-text>Нет игр с читателями</secondary-text>
    </template>
    <template v-else>
      <game-menu-link
        v-for="game in store.popularGames"
        :key="game.id"
        :game="game"
        :counters="true"
        :alwaysShowCounters="!userStore.user"
      />
    </template>
  </menu-block>
</template>

<script setup lang="ts">
import MenuBlock from "@/widgets/menu/MenuBlock.vue";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";
import GameMenuLink from "@/widgets/menu/GameMenuLink.vue";
import { useGamesStore } from "@/entities/game";
import { useUserStore } from "@/entities/user";
import { onMounted } from "vue";

const store = useGamesStore();
const userStore = useUserStore();

onMounted(() => store.fetchPopularGames());
</script>
