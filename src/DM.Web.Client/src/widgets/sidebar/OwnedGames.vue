<template>
  <SidebarBlock token="OwnedGames">
    <template #title>Мои игры</template>
    <SidebarSkeleton
      v-if="store.participatingGamesLoading && !store.participatingGames"
      :lines="5"
    />
    <li v-else-if="store.participatingGamesError">
      <SecondaryText class="error">Не удалось загрузить</SecondaryText>
    </li>
    <li
      v-else-if="
        !store.participatingGames || store.participatingGames.length === 0
      "
    >
      <SecondaryText>У вас пока нет игр</SecondaryText>
    </li>
    <template v-else>
      <!-- Mentored games (mentor role) -->
      <template v-if="mentorGames.length > 0">
        <SidebarGameLink
          v-for="game in mentorGames"
          :key="game.id"
          :game="game"
          :counters="true"
          :always-show-counters="true"
          prefix="~ "
        />
      </template>
      <!-- Separator between mentored and owned -->
      <li
        v-if="
          mentorGames.length > 0 &&
          (ownedGames.length > 0 ||
            playingGames.length > 0 ||
            readingGames.length > 0)
        "
        aria-hidden="true"
      >
        <DashSeparator spacing="none" width="75%" />
      </li>
      <!-- Owned games (Master, Assistant) -->
      <template v-if="ownedGames.length > 0">
        <SidebarGameLink
          v-for="game in ownedGames"
          :key="game.id"
          :game="game"
          :counters="true"
          :always-show-counters="true"
        />
      </template>
      <!-- Separator between owned and playing -->
      <li
        v-if="ownedGames.length > 0 && playingGames.length > 0"
        aria-hidden="true"
      >
        <DashSeparator spacing="none" width="75%" />
      </li>
      <!-- Playing games (Player role) -->
      <template v-if="playingGames.length > 0">
        <SidebarGameLink
          v-for="game in playingGames"
          :key="game.id"
          :game="game"
          :counters="true"
          :always-show-counters="true"
        />
      </template>
      <!-- Separator between playing and reading -->
      <li
        v-if="
          (ownedGames.length > 0 || playingGames.length > 0) &&
          readingGames.length > 0
        "
        aria-hidden="true"
      >
        <DashSeparator spacing="none" width="75%" />
      </li>
      <!-- Reading games (Reader role) -->
      <template v-if="readingGames.length > 0">
        <SidebarGameLink
          v-for="game in readingGames"
          :key="game.id"
          :game="game"
          :counters="true"
          :always-show-counters="true"
        />
      </template>
    </template>
    <li aria-hidden="true">
      <DashSeparator spacing="none" width="75%" />
    </li>
    <li>
      <span class="muted" aria-hidden="true">- </span>
      <router-link
        class="forward"
        :to="{ name: 'games', query: { status: 'Active' } }"
        >Все активные игры</router-link
      >
    </li>
    <li>
      <span class="muted" aria-hidden="true">- </span>
      <router-link class="forward" :to="{ name: 'create-game' }"
        >Создать игру</router-link
      >
    </li>
  </SidebarBlock>
</template>

<script setup lang="ts">
import SidebarBlock from "./SidebarBlock.vue";
import SidebarSkeleton from "./SidebarSkeleton.vue";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";
import SidebarGameLink from "./SidebarGameLink.vue";
import { DashSeparator } from "@/shared/ui/DashSeparator";
import { useAuthStore } from "@/entities/user";
import {
  useGamesStore,
  GameParticipation,
  type GameRef,
} from "@/entities/game";
import { computed } from "vue";
import { useViewerChange } from "@/shared/lib/composables/useViewerChange";

import { onMounted } from "vue";

const userStore = useAuthStore();
const store = useGamesStore();

// Owner participation (without the mentor flag - handled separately).
// Authority covers master and assistant; Owner is the master alone.
const ownerFlags: GameParticipation[] = [
  GameParticipation.Owner,
  GameParticipation.Authority,
];

// Participation check helpers with null safety
const hasRole = (game: GameRef, flag: GameParticipation) =>
  game.participation?.includes(flag) ?? false;
const hasAnyOwnerRole = (game: GameRef) =>
  game.participation?.some((p) => ownerFlags.includes(p)) ?? false;

// Mentored (Mentor, but not Master/Assistant)
const mentorGames = computed(
  () =>
    store.participatingGames?.filter(
      (game) =>
        hasRole(game, GameParticipation.Moderator) && !hasAnyOwnerRole(game),
    ) ?? [],
);

// Owned (Master, Assistant)
const ownedGames = computed(
  () => store.participatingGames?.filter(hasAnyOwnerRole) ?? [],
);

// Playing (Player, but not Master/Assistant/Mentor)
const playingGames = computed(
  () =>
    store.participatingGames?.filter(
      (game) =>
        hasRole(game, GameParticipation.Player) && !hasAnyOwnerRole(game),
    ) ?? [],
);

// Reading (Reader, but not Player/Master/Assistant/Mentor)
const readingGames = computed(
  () =>
    store.participatingGames?.filter(
      (game) =>
        hasRole(game, GameParticipation.Reader) &&
        !hasRole(game, GameParticipation.Player) &&
        !hasAnyOwnerRole(game),
    ) ?? [],
);

// Initial fetch on mount only runs for logged-in users (the component
// template is gated by `v-if="userStore.user"` in LeftSidebar). Using
// onMounted instead of a watch with `immediate: true` avoids firing
// the fetch before the user store is fully hydrated, and matches the
// pattern used by the other sidebar widgets. Subsequent login/logout
// transitions are handled by the watch below.
onMounted(() => {
  if (userStore.user?.username) {
    store.fetchParticipatingGames();
  }
});

// Reset first in both directions, then load for whoever is here now. The old
// pair of branches asked whether a name had appeared or disappeared, and a
// second tab signing another account in moves it from A to B in one step: the
// list of the previous viewer, private games included, stayed under the new
// name. Resetting before the fetch matters for the same reason - the request
// takes time, and the old rows must not be on screen while it runs.
useViewerChange((username) => {
  store.resetParticipatingGames();
  if (username) store.fetchParticipatingGames();
});
</script>

<style scoped lang="sass">
.muted
  color: $text-muted

.forward
  font-weight: bold

.error
  color: $accent-red
</style>
