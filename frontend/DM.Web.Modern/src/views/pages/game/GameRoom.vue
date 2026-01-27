<script setup lang="ts">
import { computed, watch } from "vue";
import { useRoute, useRouter } from "vue-router";
import { storeToRefs } from "pinia";
import { useGameDetailsStore } from "@/stores";
import { extractNumberParam } from "@/router";
import { useFetchData } from "@/composables/useFetchData";
import TheLoader from "@/components/TheLoader.vue";
import ThePaging from "@/components/ThePaging.vue";
import SecondaryText from "@/components/layout/SecondaryText.vue";
import BlockTitle from "@/components/layout/BlockTitle.vue";
import TheIcon from "@/components/icons/TheIcon.vue";
import { IconType } from "@/components/icons/iconType";
import GamePost from "./GamePost.vue";

const route = useRoute();
const router = useRouter();
const gameStore = useGameDetailsStore();
const {
  game,
  currentRoom,
  posts,
  postsPaging,
  postsLoading,
  postsError,
} = storeToRefs(gameStore);

const roomId = computed(() => route.params.roomId as string);
const currentPage = computed(() => extractNumberParam(route.params.n));

function handlePageChange(page: number) {
  router.push({
    name: "game-room",
    params: {
      id: game.value?.id,
      roomId: roomId.value,
      n: page > 1 ? page : undefined,
    },
  });
}

useFetchData(
  () => gameStore.loadPosts(roomId.value, currentPage.value),
  [
    {
      param: (p) => p.roomId,
      callback: (id) => gameStore.loadPosts(id as string, 1),
    },
    {
      param: (p) => p.n,
      callback: (n) => gameStore.loadPosts(roomId.value, extractNumberParam(n)),
    },
  ],
);
</script>

<template>
  <div class="game-room">
    <!-- Back link -->
    <router-link
      :to="{ name: 'game-rooms', params: { id: game?.id } }"
      class="back-link"
    >
      <the-icon :font="IconType.ArrowLeft" />
      Назад к комнатам
    </router-link>

    <!-- Room header -->
    <block-title v-if="currentRoom">
      {{ currentRoom.title }}
    </block-title>

    <!-- Loading -->
    <the-loader v-if="postsLoading" />

    <!-- Error -->
    <div v-else-if="postsError" class="posts-error">
      {{ postsError }}
    </div>

    <!-- Empty -->
    <div v-else-if="posts.length === 0" class="posts-empty">
      <secondary-text>В этой комнате пока нет постов</secondary-text>
    </div>

    <!-- Posts list -->
    <div v-else class="posts-list">
      <game-post v-for="post in posts" :key="post.id" :post="post" />
    </div>

    <!-- Paging -->
    <the-paging
      v-if="postsPaging && postsPaging.pagesCount > 1"
      :current="postsPaging.currentPage"
      :total="postsPaging.pagesCount"
      @change="handlePageChange"
    />
  </div>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Variables"
@import "src/assets/styles/Themes"

.game-room
  min-height: $grid-step * 50

.back-link
  display: inline-flex
  align-items: center
  gap: $tiny
  margin-bottom: $medium
  color: $link
  text-decoration: none

  &:hover
    text-decoration: underline

.posts-error,
.posts-empty
  padding: $big
  text-align: center

.posts-error
  color: $accent-red

.posts-list
  display: flex
  flex-direction: column
  gap: $medium
</style>
