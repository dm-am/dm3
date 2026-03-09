<script setup lang="ts">
import { computed, watch } from "vue";
import { useRoute } from "vue-router";
import { storeToRefs } from "pinia";
import { useGamesStore, GameStatus } from "@/entities/game";
import ThePaging from "@/shared/ui/Paging/ThePaging.vue";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";
import { UserLink } from "@/entities/user";
import HumanDate from "@/shared/ui/Date/HumanDate.vue";

const route = useRoute();
const gamesStore = useGamesStore();
const {
  activeGamesPage,
  recruitingGamesPage,
  finishedGamesPage,
  moderationGamesPage,
  activeGamesLoading,
  recruitingGamesLoading,
  finishedGamesLoading,
  moderationGamesPageLoading,
} = storeToRefs(gamesStore);

const status = computed(() => route.meta.status as string);

const currentGames = computed(() => {
  switch (status.value) {
    case "active":
      return activeGamesPage.value;
    case "recruiting":
      return recruitingGamesPage.value;
    case "finished":
      return finishedGamesPage.value;
    case "moderation":
      return moderationGamesPage.value;
    default:
      return null;
  }
});

const isLoading = computed(() => {
  switch (status.value) {
    case "active":
      return activeGamesLoading.value;
    case "recruiting":
      return recruitingGamesLoading.value;
    case "finished":
      return finishedGamesLoading.value;
    case "moderation":
      return moderationGamesPageLoading.value;
    default:
      return false;
  }
});

const routeName = computed(() => {
  switch (status.value) {
    case "active":
      return "games-active";
    case "recruiting":
      return "games-recruiting";
    case "finished":
      return "games-finished";
    case "moderation":
      return "games-moderation";
    default:
      return "games-active";
  }
});

const statusLabels: Record<string, string> = {
  [GameStatus.Draft]: "Черновик",
  [GameStatus.Active]: "Активная",
  [GameStatus.Closed]: "Закрыта",
};

function formatStatus(gameStatus: string): string {
  return statusLabels[gameStatus] || gameStatus;
}

function formatTags(tags: { title: string }[]): string {
  if (!tags?.length) return "—";
  return tags
    .slice(0, 3)
    .map((t) => t.title)
    .join(", ");
}

function fetchGames() {
  switch (status.value) {
    case "active":
      gamesStore.fetchActiveGames();
      break;
    case "recruiting":
      gamesStore.fetchRecruitingGames();
      break;
    case "finished":
      gamesStore.fetchFinishedGames();
      break;
    case "moderation":
      gamesStore.fetchModerationGamesPage();
      break;
  }
}

watch(
  () => route.meta.status,
  () => {
    fetchGames();
  },
  { immediate: true },
);
</script>

<template>
  <the-paging
    v-if="currentGames?.paging"
    :paging="currentGames.paging"
    :to="{ name: routeName }"
  />

  <div class="games-table">
    <div class="games-header">
      <div class="col-title">Название</div>
      <div class="col-master">Мастер</div>
      <div class="col-system">Система</div>
      <div class="col-tags">Теги</div>
      <div class="col-date">Дата</div>
    </div>

    <secondary-text
      v-if="!currentGames?.resources?.length"
      class="games-empty"
    >
      Нет игр в этой категории
    </secondary-text>
    <template v-else>
      <div
        v-for="game in currentGames.resources"
        :key="game.id"
        class="games-row"
      >
        <div class="col-title">
          <router-link :to="{ name: 'game', params: { id: game.id } }">
            {{ game.title }}
          </router-link>
          <span class="game-status">{{ formatStatus(game.status) }}</span>
        </div>
        <div class="col-master">
          <user-link v-if="game.master" :user="game.master" />
          <span v-else>—</span>
        </div>
        <div class="col-system">
          {{ game.system || "—" }}
        </div>
        <div class="col-tags">
          {{ formatTags(game.tags) }}
        </div>
        <div class="col-date">
          <human-date
            v-if="game.released"
            :date="game.released"
            format="DD.MM.YYYY"
          />
          <span v-else>—</span>
        </div>
      </div>
    </template>
  </div>
</template>

<style scoped lang="sass">
@import "@/assets/styles/Themes"
@import "@/assets/styles/Tables"

.games-table
  width: 100%
  +table

.games-header,
.games-row
  display: grid
  grid-template-columns: 1fr 140px 150px 150px 100px
  align-items: center
  +table-columns

.games-header
  +table-header
  font-weight: normal

  .col-master,
  .col-system,
  .col-tags,
  .col-date
    text-align: center

.games-row
  +table-row

  .col-title
    a
      color: $link
      &:hover
        color: $link-hover

  .game-status
    margin-left: $tiny
    font-size: $secondary-font-size
    color: $text-muted

  .col-master
    text-align: center

  .col-system
    text-align: center
    color: $text-muted

  .col-tags
    text-align: center
    font-size: $secondary-font-size
    color: $text-muted

  .col-date
    text-align: center
    color: $text-muted

.games-empty
  padding: $big
  text-align: center
</style>
