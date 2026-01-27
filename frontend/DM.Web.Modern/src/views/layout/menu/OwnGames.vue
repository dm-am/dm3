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
    <the-loader v-else-if="!store.ownGames" />
    <template v-else-if="store.ownGames.length === 0">
      <secondary-text>Пока тут ничего нет...</secondary-text>
    </template>
    <template v-else>
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
import TheLoader from "@/components/TheLoader.vue";
import SecondaryText from "@/components/layout/SecondaryText.vue";
import GameMenuLink from "@/views/layout/menu/GameMenuLink.vue";
import { useUserStore } from "@/stores";
import { useGamesStore } from "@/stores/games";
import { computed, watch } from "vue";
import { GameParticipation, GameStatus, type Game } from "@/api/models/gaming";
import GamesList from "@/views/layout/menu/GamesList.vue";

const userStore = useUserStore();
const store = useGamesStore();

// Роли владения игрой
const ownerRoles = [
  GameParticipation.Owner,
  GameParticipation.Authority,
  GameParticipation.Moderator,
];

// Проверка участия с null safety
const hasRole = (game: Game, role: GameParticipation) =>
  game.participation?.includes(role) ?? false;
const hasAnyOwnerRole = (game: Game) =>
  game.participation?.some((p) => ownerRoles.includes(p)) ?? false;

// Ведомые (Owner, Authority, Moderator)
const ownedGames = computed(
  () => store.ownGames?.filter(hasAnyOwnerRole) ?? [],
);

// Играемые (Player, но не Owner/Authority/Moderator)
const playingGames = computed(
  () =>
    store.ownGames?.filter(
      (game) =>
        hasRole(game, GameParticipation.Player) && !hasAnyOwnerRole(game),
    ) ?? [],
);

// Читаемые (Reader, но не Player/Owner/Authority/Moderator)
const readingGames = computed(
  () =>
    store.ownGames?.filter(
      (game) =>
        hasRole(game, GameParticipation.Reader) &&
        !hasRole(game, GameParticipation.Player) &&
        !hasAnyOwnerRole(game),
    ) ?? [],
);

// Watch only for user login/logout, not data refresh
// Using user.login as the trigger prevents re-fetch when fetchUser updates the object reference
watch(
  () => userStore.user?.login,
  (newLogin, oldLogin) => {
    if (newLogin && newLogin !== oldLogin) {
      // User logged in (or first load with user from localStorage)
      store.fetchOwnGames();
    } else if (!newLogin && oldLogin) {
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
