<script setup lang="ts">
// Thin game layout: the game title (H1) and a <router-view> for the active
// sub-page. Status, system, setting, master and assistants live in the info
// table (GameDetails), not duplicated in a header strip. All per-game
// navigation and actions live in the left-sidebar GamePanel.
import { computed, onUnmounted } from "vue";
import { useRoute } from "vue-router";
import { storeToRefs } from "pinia";
import { useGameDetailsStore } from "@/entities/game";
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
import { provideZoneSection } from "@/shared/lib/composables/useZoneSection";
import PageTitle from "@/shared/ui/Layout/PageTitle.vue";
import { PageTitleSkeleton } from "@/shared/ui/Skeleton";

const route = useRoute();
const gameStore = useGameDetailsStore();
const { game, gameError, gameErrorStatus } = storeToRefs(gameStore);

// The failure of the game load, as a page rather than as a sentence: a game
// that does not exist, one the viewer may not read and a server that fell over
// were all drawn as "Не удалось загрузить игру", which reads as a network
// glitch in all three cases.
//
// Derived from the store, not kept in a local ref. The store owns "the current
// game" and every reload writes the refusal into it (the shell's own load, a
// sub-page's, the refresh a status transition runs), while a copy in the shell
// knew only about the loads the shell itself started. A game that answered 403
// to any other reload left `game` null with the local code still null, and the
// page then held its loading skeleton instead of saying anything.
//
// The status is read through the sentence and not instead of it: an error that
// carries no status leaves gameErrorStatus null, and keying the page on the
// status alone would hold that skeleton in exactly the same way. Zero is a
// status too (client.ts fills it for a request that never reached the API), so
// truthiness is not the test either.
//
// Gone is read as "не найдена", the default. GameService answers Gone for every
// id that addresses nothing this reader may see — a deleted game, a mistyped id
// and a game hidden from them are one answer — so "удалена" would be a sentence
// the server never made: a typo in the address would be reported as somebody
// having deleted the game. Only the topic endpoint separates the two, and
// goneMeansRemoved exists for it (errorConfig.ts).
const errorCode = computed(() =>
  gameError.value
    ? errorCodeForStatus(gameErrorStatus.value ?? undefined)
    : null,
);

const gameId = computed(() => route.params.id as string);

// The zone shell owns both the heading on the page and the name of the tab, and
// they are the same sentence: the game name first (it is what tells two tabs
// apart when the browser truncates it), the section of the active sub-route
// second. Composed once, so the two cannot disagree — before this the heading
// said only the game, the sub-page drew a second h1 saying "Настройки игры",
// and the tab said a third thing.
//
// A sub-route whose section is data — a character's name, an NPC sheet — cannot
// put it in meta and announces it through provideZoneSection instead.
//
// While the error page is showing, the error owns the title: there is no game
// behind the id, so the section alone would name a page the reader is not
// looking at. Same rule and same source as the forum shell (ForumPage.vue).
const announced = provideZoneSection();
const heading = computed(() =>
  joinTitleSegments(game.value?.title, announced.value ?? route.meta.section),
);

useDocumentTitle(() =>
  errorCode.value ? getErrorConfig(errorCode.value).title : heading.value,
);

async function load(id: string) {
  const { ok } = await gameStore.loadGame(id);
  // The refusal itself is already in the store, which is what the error page
  // reads. Rooms are only worth preloading for a game that opened: the error
  // page carries no navigation, and the second request would just collect its
  // own refusal.
  if (!ok) return;
  await gameStore.loadRooms(id);
}

useFetchData(
  () => load(gameId.value),
  [
    {
      param: (p) => p.id,
      callback: (id) => {
        // Wipe first. The detail store is a single bag for "the current game",
        // not keyed by id, and /game/A/... and /game/B/... share one route
        // record, so the shell never unmounts and the onUnmounted reset never
        // fires on a sidebar click. Without this, posts, characters, comments,
        // notepad and blacklist of the previous game stayed on screen under the
        // new title until each sub-page happened to refetch.
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
      <page-title>{{ heading }}</page-title>
    </div>

    <router-view />
  </template>

  <ErrorPage v-else-if="errorCode" :code="errorCode" />

  <!-- Loading: twin of the loaded header (skeleton-parity). Reuses
       .game-header so the margins match; the twin itself owns its geometry. -->
  <div v-else class="game-header">
    <PageTitleSkeleton />
  </div>
</template>

<style scoped lang="sass">
.game-header
  margin-bottom: $medium
</style>
