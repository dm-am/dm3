<script setup lang="ts">
// Game layout: the title (H1) and the router-view of the active sub-page.
import { computed, onUnmounted, watch } from "vue";
import { useRoute } from "vue-router";
import { storeToRefs } from "pinia";
import { useGameDetailsStore } from "@/entities/game";
import { useAuthStore } from "@/entities/user";
import {
  ErrorPage,
  errorCodeForStatus,
  getErrorConfig,
} from "@/shared/ui/ErrorPage";
import { useFetchData } from "@/shared/lib/composables/useFetchData";
import {
  joinTitleSegments,
  useDocumentTitle,
} from "@/shared/lib/composables/useDocumentTitle";
import {
  provideZoneMarker,
  provideZoneSection,
} from "@/shared/lib/composables/useZoneSection";
import PageTitle from "@/shared/ui/Layout/PageTitle.vue";
import { PageTitleSkeleton } from "@/shared/ui/Skeleton";

const route = useRoute();
const gameStore = useGameDetailsStore();
const { game, gameError, gameErrorStatus } = storeToRefs(gameStore);

// From the store, which every reload of the game writes, not only the shell's.
// Gone stays "не найдена": the server answers Gone for a typo too.
// Keyed on the sentence, not the status: a request that never left carries status 0.
const errorCode = computed(() =>
  gameError.value
    ? errorCodeForStatus(gameErrorStatus.value ?? undefined)
    : null,
);

const gameId = computed(() => route.params.id as string);

// One sentence for the heading and the tab, so the two cannot disagree.
const announced = provideZoneSection();
const heading = computed(() =>
  joinTitleSegments(game.value?.title, announced.value ?? route.meta.section),
);
// A state word after the heading; left out of the tab title.
const marker = provideZoneMarker();

useDocumentTitle(() =>
  errorCode.value ? getErrorConfig(errorCode.value).title : heading.value,
);

async function load(id: string) {
  const { ok } = await gameStore.loadGame(id);
  if (!ok) return;
  await gameStore.loadRooms(id);
}

// The roles arrive with the game, so another user means another fetch.
// The username, not the user: a settings update must not refetch the zone.
const { user } = storeToRefs(useAuthStore());
watch(
  () => user.value?.username ?? null,
  () => load(gameId.value),
);

useFetchData(
  () => load(gameId.value),
  [
    {
      param: (p) => p.id,
      callback: (id) => {
        // One store for the current game; the shell outlives a change of game.
        gameStore.reset();
        return load(id as string);
      },
    },
  ],
);

onUnmounted(() => {
  gameStore.reset();
});
</script>

<template>
  <template v-if="game">
    <div class="game-header">
      <!-- A text node, not a margin: the copied heading keeps the gap. -->
      <page-title
        >{{ heading
        }}<template v-if="marker"
          >{{ " " }}<span class="heading-marker">{{ marker }}</span></template
        ></page-title
      >
    </div>

    <router-view />
  </template>

  <ErrorPage v-else-if="errorCode" :code="errorCode" />

  <div v-else class="game-header">
    <PageTitleSkeleton />
  </div>
</template>

<style scoped lang="sass">
.game-header
  margin-bottom: $medium

.heading-marker
  font-size: $secondary-font-size
  font-weight: normal
  color: $text-muted
  text-transform: uppercase
</style>
