<template>
  <template v-if="userStore.user">
    <own-games />
  </template>

  <games-list
    title="Набор игроков"
    link-text="Все игры с открытым набором"
    token="RecruitingGames"
    :game-status="GameStatus.Active"
  >
    <template v-if="!gamesStore.recruitingGames || gamesStore.recruitingGames.length === 0">
      <secondary-text>Нет игр с открытым набором</secondary-text>
    </template>
    <game-menu-link
      v-else
      v-for="game in gamesStore.recruitingGames"
      :key="game.id"
      :game="game"
      :counters="true"
      :alwaysShowCounters="!userStore.user"
    />
  </games-list>

  <games-list
    v-if="!userStore.user"
    title="Активные игры"
    link-text="Все активные игры"
    token="ActiveGames"
    :game-status="GameStatus.Active"
  >
    <template v-if="!gamesStore.activeGames || gamesStore.activeGames.length === 0">
      <secondary-text>Нет активных игр</secondary-text>
    </template>
    <game-menu-link
      v-else
      v-for="game in gamesStore.activeGames"
      :key="game.id"
      :game="game"
      :counters="true"
      :alwaysShowCounters="true"
    />
  </games-list>

  <games-list
    title="Завершенные игры"
    link-text="Все завершенные игры"
    token="FinishedGames"
    :game-status="GameStatus.Closed"
  >
    <template v-if="!gamesStore.finishedGames || gamesStore.finishedGames.length === 0">
      <secondary-text>Нет завершенных игр</secondary-text>
    </template>
    <game-menu-link
      v-else
      v-for="game in gamesStore.finishedGames"
      :key="game.id"
      :game="game"
      :counters="true"
      :alwaysShowCounters="!userStore.user"
    />
  </games-list>

  <menu-block token="ActiveBlogs">
    <template #title>Активные блоги</template>
    <template v-if="!blogsStore.activeBlogs || blogsStore.activeBlogs.length === 0">
      <secondary-text>Нет активных блогов</secondary-text>
    </template>
    <template v-else>
      <div v-for="blog in blogsStore.activeBlogs" :key="blog.id">
        <span class="muted">- </span>
        <router-link :to="{ name: 'blogs' }">{{ blog.title }}</router-link>
        <span class="blog-counters"><span class="muted"> (</span>{{ blog.publicationCount || 0 }}<span class="muted">/</span>{{ blog.commentsCount || 0 }}<span class="muted">)</span></span>
      </div>
    </template>
    <div class="separator">
      - - - - - - - - - - - - - - - - - - - - - - - - - -
    </div>
    <div>
      <span class="muted">- </span>
      <router-link class="forward" :to="{ name: 'blogs' }">Все блоги</router-link>
    </div>
  </menu-block>

  <forums-list />
</template>

<script setup lang="ts">
import ForumsList from "./ForumsList.vue";
import OwnGames from "./OwnGames.vue";
import GamesList from "./GamesList.vue";
import GameMenuLink from "./GameMenuLink.vue";
import MenuBlock from "./MenuBlock.vue";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";
import { useUserStore } from "@/entities/user";
import { useGamesStore, GameStatus } from "@/entities/game";
import { useBlogsStore } from "@/entities/blog";
import { onMounted, watch } from "vue";

const userStore = useUserStore();
const gamesStore = useGamesStore();
const blogsStore = useBlogsStore();

onMounted(() => {
  if (!userStore.user) {
    gamesStore.fetchActiveGames();
  }
  gamesStore.fetchRecruitingGames();
  gamesStore.fetchFinishedGames();
  blogsStore.fetchActiveBlogs();
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

<style scoped lang="sass">
@import "src/assets/styles/Themes"

.muted
  color: $text-muted

.forward
  font-weight: bold

.separator
  color: $text-muted

.blog-counters .muted
  user-select: text
</style>
