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
import {
  joinTitleSegments,
  useDocumentTitle,
} from "@/shared/lib/composables/useDocumentTitle";
import PageTitle from "@/shared/ui/Layout/PageTitle.vue";
import { PageTitleSkeleton } from "@/shared/ui/Skeleton";

const route = useRoute();
const gameStore = useGameDetailsStore();
const { game, gameError } = storeToRefs(gameStore);

const gameId = computed(() => route.params.id as string);

// The zone shell owns the tab title for every /game/:id route: the game name
// first (it is what tells two tabs apart when the browser truncates), the
// section of the active sub-route second. Sub-routes whose section is data —
// a room, a chat room — declare meta.dynamicTitle and compose it themselves.
useDocumentTitle(() =>
  joinTitleSegments(game.value?.title, route.meta.section),
);

useFetchData(async () => {
  await gameStore.loadGame(gameId.value);
  // Also preload rooms for navigation
  await gameStore.loadRooms(gameId.value);
}, [
  {
    param: (p) => p.id,
    callback: async (id) => {
      // Wipe first. The detail store is a single bag for "the current game", not
      // keyed by id, and /game/A/... and /game/B/... share one route record, so
      // the shell never unmounts and the onUnmounted reset never fires on a
      // sidebar click. Without this, posts, characters, comments, notepad and
      // blacklist of the previous game stayed on screen under the new title
      // until each sub-page happened to refetch.
      gameStore.reset();
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
       .game-header so the margins match; the twin itself owns its geometry. -->
  <div v-else class="game-header">
    <PageTitleSkeleton />
  </div>
</template>

<style scoped lang="sass">
.game-header
  margin-bottom: $medium

.game-error
  padding: $big
  color: $accent-red

  a
    color: $link
    margin-top: $small
    display: inline-block
</style>
