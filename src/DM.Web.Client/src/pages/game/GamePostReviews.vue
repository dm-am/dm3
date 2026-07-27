<script setup lang="ts">
// "Оцененные посты" — rated posts (posts with at least one review) of a single
// game. The rated-posts endpoint filters by gameId only (no room param), so
// the Комната filter is applied client-side over the fetched set. Rated posts
// are a small subset of a game's posts, so a single generous page covers them.
import { computed, ref, watch } from "vue";
import { useRoute } from "vue-router";
import { storeToRefs } from "pinia";
import { useGameDetailsStore, gameApi } from "@/entities/game";
import type { Post } from "@/entities/game";
import { GamePost } from "@/widgets/game-post";
import { GamePostSkeleton } from "@/shared/ui/Skeleton";
import { Select, type SelectOption } from "@/shared/ui/Select";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";
import { ErrorState } from "@/shared/ui/ErrorState";

const route = useRoute();
const gameStore = useGameDetailsStore();
const { game, rooms } = storeToRefs(gameStore);

const gameId = computed(() => game.value?.id ?? (route.params.id as string));

const posts = ref<Post[]>([]);
const loading = ref(false);
const loadError = ref<string | null>(null);

// "" = all rooms. Otherwise a room id from the store rooms list.
const roomFilter = ref<string>("");

const roomOptions = computed<SelectOption[]>(() => [
  { value: "", label: "Все комнаты" },
  ...rooms.value.map((r) => ({
    value: r.id as string,
    label: r.title,
  })),
]);

const filteredPosts = computed(() =>
  roomFilter.value
    ? posts.value.filter((p) => p.room?.id === roomFilter.value)
    : posts.value,
);

async function fetch() {
  if (!gameId.value) return;
  loading.value = true;
  try {
    const { data, error } = await gameApi.getRatedPosts({
      gameId: gameId.value,
      hasReviews: true,
      sortBy: "lastreview",
      sortOrder: "desc",
      take: 100,
    });
    if (error) {
      loadError.value = "Не удалось загрузить оцененные посты";
    } else {
      loadError.value = null;
      posts.value = data?.resources ?? [];
    }
  } finally {
    loading.value = false;
  }
}

watch(gameId, fetch, { immediate: true });

const isEmpty = computed(
  () => !loading.value && filteredPosts.value.length === 0,
);
</script>

<template>
  <div class="game-post-reviews">
    <div class="filters">
      <Select
        v-model="roomFilter"
        :options="roomOptions"
        placeholder="Все комнаты"
        class="room-filter"
      />
    </div>

    <ErrorState
      v-if="loadError"
      class="error-banner"
      :message="loadError"
      :retry="fetch"
    />

    <GamePostSkeleton v-if="loading && !posts.length" :count="5" />

    <SecondaryText v-else-if="isEmpty">
      {{
        roomFilter
          ? "В этой комнате нет оцененных постов"
          : "В этой игре пока нет оцененных постов"
      }}
    </SecondaryText>

    <div v-else class="posts-list">
      <GamePost
        v-for="post in filteredPosts"
        :key="post.id"
        :post="post"
        show-navigation
      />
    </div>
  </div>
</template>

<style scoped lang="sass">
.game-post-reviews
  display: flex
  flex-direction: column
  gap: $small
  min-height: $grid-step * 50

.filters
  margin-bottom: $small

.room-filter
  max-width: 320px

.error-banner
  margin-bottom: $medium

.posts-list
  display: flex
  flex-direction: column
  gap: $medium
</style>
