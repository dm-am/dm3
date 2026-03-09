<template>
  <games-list
    title="Мои игры"
    link-text="Все активные игры"
    token="OwnGames"
    :game-status="GameStatus.Active"
    :show-create-link="true"
  >
    <secondary-text v-if="store.ownGamesError" class="error">
      {{ store.ownGamesError.title || "Ошибка загрузки" }}
    </secondary-text>
    <template v-else-if="!store.ownGames || store.ownGames.length === 0">
      <secondary-text>У вас нет игр</secondary-text>
    </template>
    <template v-else>
      <!-- Курируемые игры (ментор) -->
      <template v-if="mentorGames.length > 0">
        <game-menu-link
          v-for="game in mentorGames"
          :key="game.id"
          :game="game"
          :counters="true"
          :always-show-counters="true"
          prefix="~ "
        />
      </template>
      <!-- Разделитель между курируемыми и ведомыми -->
      <div
        v-if="mentorGames.length > 0 && (ownedGames.length > 0 || playingGames.length > 0 || readingGames.length > 0)"
        class="separator"
      >
        - - - - - - - - - - - - - - - - - - - - - - - - - -
      </div>
      <!-- Ведомые игры -->
      <template v-if="ownedGames.length > 0">
        <game-menu-link
          v-for="game in ownedGames"
          :key="game.id"
          :game="game"
          :counters="true"
          :always-show-counters="true"
        />
      </template>
      <!-- Разделитель между ведомыми и играемыми -->
      <div
        v-if="ownedGames.length > 0 && playingGames.length > 0"
        class="separator"
      >
        - - - - - - - - - - - - - - - - - - - - - - - - - -
      </div>
      <!-- Играемые игры -->
      <template v-if="playingGames.length > 0">
        <game-menu-link
          v-for="game in playingGames"
          :key="game.id"
          :game="game"
          :counters="true"
          :always-show-counters="true"
        />
      </template>
      <!-- Разделитель между играемыми и читаемыми -->
      <div
        v-if="
          (ownedGames.length > 0 || playingGames.length > 0) &&
          readingGames.length > 0
        "
        class="separator"
      >
        - - - - - - - - - - - - - - - - - - - - - - - - - -
      </div>
      <!-- Читаемые игры -->
      <template v-if="readingGames.length > 0">
        <game-menu-link
          v-for="game in readingGames"
          :key="game.id"
          :game="game"
          :counters="true"
          :always-show-counters="true"
        />
      </template>
    </template>
  </games-list>
</template>

<script setup lang="ts">
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";
import GameMenuLink from "./GameMenuLink.vue";
import { useUserStore } from "@/entities/user";
import { useGamesStore, GameRole, GameStatus, type Game } from "@/entities/game";
import { computed, watch } from "vue";
import GamesList from "./GamesList.vue";

const userStore = useUserStore();
const store = useGamesStore();

// Роли владения игрой (без Mentor - он отдельно для менторов)
const ownerRoles = [
  GameRole.Master,
  GameRole.Assistant,
];

// Проверка участия с null safety
const hasRole = (game: Game, role: GameRole) =>
  game.roles?.includes(role) ?? false;
const hasAnyOwnerRole = (game: Game) =>
  game.roles?.some((p) => ownerRoles.includes(p)) ?? false;

// Курируемые (Mentor, но не Master/Assistant)
const mentorGames = computed(
  () =>
    store.ownGames?.filter(
      (game) =>
        hasRole(game, GameRole.Mentor) && !hasAnyOwnerRole(game),
    ) ?? [],
);

// Ведомые (Master, Assistant)
const ownedGames = computed(
  () => store.ownGames?.filter(hasAnyOwnerRole) ?? [],
);

// Играемые (Player, но не Master/Assistant/Mentor)
const playingGames = computed(
  () =>
    store.ownGames?.filter(
      (game) =>
        hasRole(game, GameRole.Player) && !hasAnyOwnerRole(game),
    ) ?? [],
);

// Читаемые (Reader, но не Player/Master/Assistant/Mentor)
const readingGames = computed(
  () =>
    store.ownGames?.filter(
      (game) =>
        hasRole(game, GameRole.Reader) &&
        !hasRole(game, GameRole.Player) &&
        !hasAnyOwnerRole(game),
    ) ?? [],
);

// Watch only for user login/logout, not data refresh
// Using user.username as the trigger prevents re-fetch when fetchUser updates the object reference
watch(
  () => userStore.user?.username,
  (newUsername, oldUsername) => {
    if (newUsername && newUsername !== oldUsername) {
      // User logged in (or first load with user from localStorage)
      store.fetchOwnGames();
    } else if (!newUsername && oldUsername) {
      // User logged out
      store.resetOwnGames();
    }
  },
  { immediate: true },
);
</script>

<style scoped lang="sass">
@import "src/assets/styles/Themes"

.separator
  color: $text-muted

.error
  color: $accent-red
</style>
