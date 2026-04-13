<template>
  <SidebarBlock token="OwnedGames">
    <template #title>Мои игры</template>
    <SidebarSkeleton v-if="store.participatingGamesLoading && !store.participatingGames" :lines="5" />
    <SecondaryText v-else-if="store.participatingGamesError" class="error">
      {{ store.participatingGamesError.title || "Ошибка загрузки" }}
    </SecondaryText>
    <template v-else-if="!store.participatingGames || store.participatingGames.length === 0">
      <SecondaryText>У вас нет игр</SecondaryText>
    </template>
    <template v-else>
      <!-- Mentored games (mentor role) -->
      <template v-if="mentorGames.length > 0">
        <GameLink
          v-for="game in mentorGames"
          :key="game.id"
          :game="game"
          :counters="true"
          :always-show-counters="true"
          prefix="~ "
        />
      </template>
      <!-- Separator between mentored and owned -->
      <div
        v-if="
          mentorGames.length > 0 &&
          (ownedGames.length > 0 ||
            playingGames.length > 0 ||
            readingGames.length > 0)
        "
        class="separator"
      >
        - - - - - - - - - - - - - - - - - - - - - - - - - -
      </div>
      <!-- Owned games (Master, Assistant) -->
      <template v-if="ownedGames.length > 0">
        <GameLink
          v-for="game in ownedGames"
          :key="game.id"
          :game="game"
          :counters="true"
          :always-show-counters="true"
        />
      </template>
      <!-- Separator between owned and playing -->
      <div
        v-if="ownedGames.length > 0 && playingGames.length > 0"
        class="separator"
      >
        - - - - - - - - - - - - - - - - - - - - - - - - - -
      </div>
      <!-- Playing games (Player role) -->
      <template v-if="playingGames.length > 0">
        <GameLink
          v-for="game in playingGames"
          :key="game.id"
          :game="game"
          :counters="true"
          :always-show-counters="true"
        />
      </template>
      <!-- Separator between playing and reading -->
      <div
        v-if="
          (ownedGames.length > 0 || playingGames.length > 0) &&
          readingGames.length > 0
        "
        class="separator"
      >
        - - - - - - - - - - - - - - - - - - - - - - - - - -
      </div>
      <!-- Reading games (Reader role) -->
      <template v-if="readingGames.length > 0">
        <GameLink
          v-for="game in readingGames"
          :key="game.id"
          :game="game"
          :counters="true"
          :always-show-counters="true"
        />
      </template>
    </template>
    <div class="separator">
      - - - - - - - - - - - - - - - - - - - - - - - - - -
    </div>
    <div>
      <span class="muted">- </span>
      <router-link
        class="forward"
        :to="{ name: 'games', query: { status: 'Active' } }"
        >Все активные игры</router-link
      >
    </div>
    <div>
      <span class="muted">- </span>
      <router-link class="forward" :to="{ name: 'create-game' }"
        >Создать игру</router-link
      >
    </div>
  </SidebarBlock>
</template>

<script setup lang="ts">
import SidebarBlock from "./SidebarBlock.vue";
import SidebarSkeleton from "./SidebarSkeleton.vue";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";
import GameLink from "./GameLink.vue";
import { useUserStore } from "@/entities/user";
import { useGamesStore, GameRole, type GameRef } from "@/entities/game";
import { computed, watch } from "vue";

import { onMounted } from "vue";

const userStore = useUserStore();
const store = useGamesStore();

// Owner roles (without Mentor - handled separately)
const ownerRoles = [GameRole.Master, GameRole.Assistant];

// Role check helpers with null safety
const hasRole = (game: GameRef, role: GameRole) =>
  game.participation?.includes(role) ?? false;
const hasAnyOwnerRole = (game: GameRef) =>
  game.participation?.some((p) => ownerRoles.includes(p)) ?? false;

// Mentored (Mentor, but not Master/Assistant)
const mentorGames = computed(
  () =>
    store.participatingGames?.filter(
      (game) => hasRole(game, GameRole.Mentor) && !hasAnyOwnerRole(game),
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
      (game) => hasRole(game, GameRole.Player) && !hasAnyOwnerRole(game),
    ) ?? [],
);

// Reading (Reader, but not Player/Master/Assistant/Mentor)
const readingGames = computed(
  () =>
    store.participatingGames?.filter(
      (game) =>
        hasRole(game, GameRole.Reader) &&
        !hasRole(game, GameRole.Player) &&
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

watch(
  () => userStore.user?.username,
  (newUsername, oldUsername) => {
    if (!oldUsername && newUsername) {
      // User just logged in (already handled on mount if hydrated,
      // but this covers the login-without-reload path).
      store.fetchParticipatingGames();
    } else if (oldUsername && !newUsername) {
      store.resetParticipatingGames();
    }
  },
);
</script>

<style scoped lang="sass">
@import "src/assets/styles/Themes"

.muted
  color: $text-muted

.forward
  font-weight: bold

.separator
  color: $text-muted

.error
  color: $accent-red
</style>
