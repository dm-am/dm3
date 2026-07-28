<script setup lang="ts">
// Thin game layout: the game title (H1) and a <router-view> for the active
// sub-page. Status, system, setting, master and assistants live in the info
// table (GameDetails), not duplicated in a header strip. All per-game
// navigation and actions live in the left-sidebar GamePanel.
import { computed, onUnmounted } from "vue";
import { useRoute } from "vue-router";
import { storeToRefs } from "pinia";
import { useGameDetailsStore } from "@/entities/game";
import { useFetchData } from "@/shared/lib/composables/useFetchData";
import PageTitle from "@/shared/ui/Layout/PageTitle.vue";

const route = useRoute();
const gameStore = useGameDetailsStore();
const { game, gameError } = storeToRefs(gameStore);

const gameId = computed(() => route.params.id as string);

useFetchData(async () => {
  await gameStore.loadGame(gameId.value);
  // Also preload rooms for navigation
  await gameStore.loadRooms(gameId.value);
}, [
  {
    param: (p) => p.id,
    callback: async (id) => {
      await gameStore.loadGame(id as string);
      await gameStore.loadRooms(id as string);
    },
  },
]);

onUnmounted(() => {
  gameStore.reset();
});
</script>

<template>
  <template v-if="game">
    <div class="game-header">
      <page-title>{{ game.title }}</page-title>
    </div>

    <router-view />
  </template>

  <div v-else-if="gameError" class="game-error">
    <p>{{ gameError }}</p>
    <router-link to="/games">Вернуться к списку игр</router-link>
  </div>

  <!-- Loading: twin of the loaded header (skeleton-parity). Reuses
       .game-header so margins match; the bar height mirrors the h1 line box
       (20px font x 1.3 line-height = 26px). -->
  <div v-else class="game-header" aria-hidden="true">
    <div class="skeleton-title" />
  </div>
</template>

<style scoped lang="sass">
@import "@/assets/styles/Skeleton"

.game-header
  margin-bottom: $medium

.game-error
  padding: $big
  text-align: center
  color: $accent-red

  a
    color: $link
    margin-top: $small
    display: inline-block

// --- Loading skeleton (twin of the loaded header) ---

// Twin of the PageTitle h1: same margins ($medium 0 $small), height equals
// the h1 line box (20px font x 1.3 line-height = 26px).
.skeleton-title
  width: 260px
  height: 26px
  margin: $medium 0 $small
  +skeleton-shimmer
</style>
