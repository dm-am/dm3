<script setup lang="ts">
import { computed, watch } from "vue";
import { useRoute } from "vue-router";
import { storeToRefs } from "pinia";
import { useGameDetailsStore } from "@/entities/game";
import { useUserStore } from "@/entities/user";
import { useFetchData } from "@/shared/lib/composables/useFetchData";
import { useScrollToElement } from "@/shared/lib/composables/useScrollToElement";
import { gameApi } from "@/entities/game";
import Paging from "@/shared/ui/Paging/Paging.vue";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";
import BlockTitle from "@/shared/ui/Layout/BlockTitle.vue";
import Icon from "@/shared/ui/Icon/Icon.vue";
import { IconType } from "@/shared/ui/Icon/iconType";
import GamePost from "./GamePost.vue";

const route = useRoute();
const gameStore = useGameDetailsStore();
const userStore = useUserStore();
const { game, currentRoom, posts, postsPaging, postsLoading, postsError } =
  storeToRefs(gameStore);

const roomNum = computed(() => parseInt(route.params.num as string));
function getPage(): number {
  const page = route.query.page;
  return page ? parseInt(page as string) || 1 : 1;
}

// Scroll to target element when posts are loaded
const postsLoaded = computed(
  () => posts.value.length > 0 && !postsLoading.value,
);
useScrollToElement(postsLoaded);

// Mark room as read when posts are loaded (for authenticated users)
watch(
  postsLoaded,
  async (loaded) => {
    if (loaded && userStore.user && currentRoom.value?.id) {
      try {
        await gameApi.markRoomAsRead(currentRoom.value.id as string);
      } catch {
        // Silently ignore - non-critical operation
      }
    }
  },
  { once: true },
);

useFetchData(
  () => gameStore.loadPostsByRoomNumber(roomNum.value, getPage()),
  [
    {
      param: (p) => p.num,
      callback: () => gameStore.loadPostsByRoomNumber(roomNum.value, 1),
    },
  ],
  [
    {
      query: (q) => q.page,
      callback: () => gameStore.loadPostsByRoomNumber(roomNum.value, getPage()),
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
      <Icon :font="IconType.ArrowLeft" />
      Назад к комнатам
    </router-link>

    <!-- Room header -->
    <block-title v-if="currentRoom">
      {{ currentRoom.title }}
    </block-title>

    <!-- Error -->
    <div v-if="postsError" class="posts-error">
      {{ postsError }}
    </div>

    <!-- Empty -->
    <div v-else-if="posts.length === 0" class="posts-empty">
      <secondary-text>В этой комнате пока нет постов</secondary-text>
    </div>

    <!-- Posts list -->
    <div v-else class="posts-list">
      <game-post
        v-for="post in posts"
        :key="post.id"
        :post="post"
        :data-id="post.id"
      />
    </div>

    <!-- Paging -->
    <Paging
      v-if="postsPaging"
      :paging="postsPaging"
      :to="{ name: 'game-room', params: { id: game?.publicId || game?.id, num: roomNum } }"
      :use-query="true"
      query-key="number"
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
  gap: $small
</style>
