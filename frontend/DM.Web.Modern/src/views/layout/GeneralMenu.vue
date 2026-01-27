<template>
  <template v-if="userStore.user">
    <moderation-games v-if="userIsAdmin" />
    <own-games />
  </template>
  <games-list
    v-else
    title="Активные игры"
    link-text="Все активные игры"
    token="ActiveGames"
    :game-status="GameStatus.Active"
  >
    <the-loader v-if="!gamesStore.activeGames" />
    <template v-else-if="gamesStore.activeGames.length === 0">
      <secondary-text>Пока тут ничего нет...</secondary-text>
    </template>
    <game-menu-link
      v-else
      v-for="game in gamesStore.activeGames"
      :key="game.id"
      :game="game"
      :counters="true"
    />
  </games-list>

  <games-list
    title="Идёт набор"
    link-text="Все игры с открытым набором"
    token="RecruitingGames"
    :game-status="GameStatus.Recruiting"
  >
    <the-loader v-if="!gamesStore.recruitingGames" />
    <template v-else-if="gamesStore.recruitingGames.length === 0">
      <secondary-text>Пока тут ничего нет...</secondary-text>
    </template>
    <game-menu-link
      v-else
      v-for="game in gamesStore.recruitingGames"
      :key="game.id"
      :game="game"
      :counters="true"
    />
  </games-list>

  <games-list
    title="Завершённые игры"
    link-text="Все завершённые игры"
    token="FinishedGames"
    :game-status="GameStatus.Finished"
  >
    <the-loader v-if="!gamesStore.finishedGames" />
    <template v-else-if="gamesStore.finishedGames.length === 0">
      <secondary-text>Пока тут ничего нет...</secondary-text>
    </template>
    <game-menu-link
      v-else
      v-for="game in gamesStore.finishedGames"
      :key="game.id"
      :game="game"
      :counters="true"
    />
  </games-list>

  <forums-list />
</template>

<script setup lang="ts">
import ForumsList from "@/views/layout/menu/ForumsList.vue";
import ModerationGames from "@/views/layout/menu/ModerationGames.vue";
import OwnGames from "@/views/layout/menu/OwnGames.vue";
import GamesList from "@/views/layout/menu/GamesList.vue";
import GameMenuLink from "@/views/layout/menu/GameMenuLink.vue";
import TheLoader from "@/components/TheLoader.vue";
import SecondaryText from "@/components/layout/SecondaryText.vue";
import { useUserStore } from "@/stores";
import { useGamesStore } from "@/stores/games";
import { computed, onMounted, watch } from "vue";
import { userIsHighAuthority } from "@/api/models/community/helpers";
import { GameStatus } from "@/api/models/gaming";

const userStore = useUserStore();
const gamesStore = useGamesStore();
const userIsAdmin = computed(() => userIsHighAuthority(userStore.user));

onMounted(() => {
  if (!userStore.user) {
    gamesStore.fetchActiveGames();
  }
  gamesStore.fetchRecruitingGames();
  gamesStore.fetchFinishedGames();
});

watch(
  () => userStore.user,
  (user) => {
    if (!user) {
      gamesStore.fetchActiveGames();
    }
  },
);
</script>
