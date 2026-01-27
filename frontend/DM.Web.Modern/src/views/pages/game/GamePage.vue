<script setup lang="ts">
import { computed, onUnmounted } from "vue";
import { useRoute } from "vue-router";
import { storeToRefs } from "pinia";
import { useGameDetailsStore, useUserStore } from "@/stores";
import { useFetchData } from "@/composables/useFetchData";
import PageTitle from "@/components/layout/PageTitle.vue";
import SecondaryText from "@/components/layout/SecondaryText.vue";
import TheLoader from "@/components/TheLoader.vue";
import UserLink from "@/components/community/UserLink.vue";
import TheButton from "@/components/inputs/TheButton.vue";
import { GameParticipation, GameStatus } from "@/api/models/gaming";

const route = useRoute();
const gameStore = useGameDetailsStore();
const { user } = storeToRefs(useUserStore());
const { game, gameLoading, gameError } = storeToRefs(gameStore);

const gameId = computed(() => route.params.id as string);

const statusLabels: Record<GameStatus, string> = {
  [GameStatus.Draft]: "Черновик",
  [GameStatus.Recruiting]: "Набор игроков",
  [GameStatus.Requirement]: "Набор игроков",
  [GameStatus.Active]: "Активная",
  [GameStatus.Frozen]: "Заморожена",
  [GameStatus.Finished]: "Завершена",
  [GameStatus.Closed]: "Закрыта",
  [GameStatus.RequiresModeration]: "На модерации",
  [GameStatus.Moderation]: "На модерации",
};

const statusClass = computed(() => {
  if (!game.value) return "";
  switch (game.value.status) {
    case GameStatus.Active:
      return "status-active";
    case GameStatus.Recruiting:
    case GameStatus.Requirement:
      return "status-recruiting";
    case GameStatus.Finished:
      return "status-finished";
    case GameStatus.Frozen:
      return "status-frozen";
    default:
      return "";
  }
});

const isSubscribed = computed(() => {
  if (!game.value?.participation) return false;
  return game.value.participation.includes(GameParticipation.Reader);
});

const isPlayer = computed(() => {
  if (!game.value?.participation) return false;
  return (
    game.value.participation.includes(GameParticipation.Player) ||
    game.value.participation.includes(GameParticipation.Moderator) ||
    game.value.participation.includes(GameParticipation.Owner)
  );
});

const canSubscribe = computed(() => user.value && !isPlayer.value);

async function handleSubscribe() {
  if (isSubscribed.value) {
    await gameStore.unsubscribe();
  } else {
    await gameStore.subscribe();
  }
}

useFetchData(
  async () => {
    await gameStore.loadGame(gameId.value);
    // Also preload rooms for navigation
    await gameStore.loadRooms(gameId.value);
  },
  [
    {
      param: (p) => p.id,
      callback: async (id) => {
        await gameStore.loadGame(id as string);
        await gameStore.loadRooms(id as string);
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
      <div class="game-title-row">
        <page-title>{{ game.title }}</page-title>
        <span :class="['game-status', statusClass]">
          {{ statusLabels[game.status] }}
        </span>
      </div>
      <secondary-text class="game-meta">
        <span v-if="game.system">{{ game.system }}</span>
        <span v-if="game.system && game.setting"> / </span>
        <span v-if="game.setting">{{ game.setting }}</span>
        <span class="game-master">
          Мастер: <user-link :user="game.master" />
        </span>
        <span v-if="game.assistant" class="game-assistant">
          Помощник: <user-link :user="game.assistant" />
        </span>
      </secondary-text>
    </div>

    <nav class="game-tabs">
      <router-link :to="{ name: 'game', params: { id: game.id } }" class="tabs-link">
        Информация
      </router-link>
      <router-link :to="{ name: 'game-rooms', params: { id: game.id } }" class="tabs-link">
        Комнаты
        <span v-if="game.unreadPostsCount" class="unread-badge">
          {{ game.unreadPostsCount }}
        </span>
      </router-link>
      <router-link :to="{ name: 'game-characters', params: { id: game.id } }" class="tabs-link">
        Персонажи
        <span v-if="game.unreadCharactersCount" class="unread-badge">
          {{ game.unreadCharactersCount }}
        </span>
      </router-link>
      <router-link :to="{ name: 'game-comments', params: { id: game.id } }" class="tabs-link">
        Комментарии
        <span v-if="game.unreadCommentsCount" class="unread-badge">
          {{ game.unreadCommentsCount }}
        </span>
      </router-link>
    </nav>

    <div class="game-actions" v-if="canSubscribe">
      <the-button @click="handleSubscribe">
        {{ isSubscribed ? "Отписаться" : "Подписаться" }}
      </the-button>
    </div>

    <router-view />
  </template>

  <div v-else-if="gameLoading" class="game-loading">
    <the-loader :big="true" />
  </div>

  <div v-else-if="gameError" class="game-error">
    <p>{{ gameError }}</p>
    <router-link to="/games">Вернуться к списку игр</router-link>
  </div>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Variables"
@import "src/assets/styles/Themes"

.game-header
  margin-bottom: $medium

.game-title-row
  display: flex
  align-items: baseline
  gap: $medium
  flex-wrap: wrap

.game-status
  font-size: $secondary-font-size
  padding: 2px $small
  border-radius: $border-radius
  background-color: $bg-element
  color: $text-muted

  &.status-active
    background-color: rgba($accent-green, 0.2)
    color: $accent-green

  &.status-recruiting
    background-color: rgba($accent-blue, 0.2)
    color: $accent-blue

  &.status-finished
    background-color: rgba($text-muted, 0.2)
    color: $text-muted

  &.status-frozen
    background-color: rgba($accent-orange, 0.2)
    color: $accent-orange

.game-meta
  display: flex
  flex-wrap: wrap
  gap: $small
  margin-top: $tiny

.game-master,
.game-assistant
  margin-left: $medium

.game-tabs
  margin-bottom: $medium

  .tabs-link
    display: inline-block
    margin-right: $medium
    text-transform: uppercase
    font-weight: bold
    color: $link-nav
    text-decoration: none
    position: relative

    &:hover
      color: $link-nav-hover
      text-decoration: underline

    &.router-link-exact-active
      color: $text
      text-decoration: none
      cursor: default

.unread-badge
  display: inline-block
  min-width: $grid-step * 4
  padding: 0 $tiny
  margin-left: $tiny
  font-size: $tertiary-font-size
  font-weight: bold
  text-align: center
  border-radius: $border-radius
  background-color: $accent-red
  color: white

.game-actions
  margin-bottom: $medium

.game-loading,
.game-error
  padding: $big
  text-align: center

.game-error
  color: $accent-red

  a
    color: $link
    margin-top: $small
    display: inline-block
</style>
