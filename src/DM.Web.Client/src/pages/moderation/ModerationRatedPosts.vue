<script setup lang="ts">
/**
 * ModerationRatedPosts — "Последние оцененные посты" (doc 4.2.3.8.4).
 * Cross-game worklist of posts that received reviews, ordered by the
 * moment of the last review (newest first). Reuses the same rated-posts
 * endpoint and GamePost rendering as the per-game "Оцененные посты"
 * page (pages/game/GamePostReviews.vue), without the game filter.
 */
import { computed, onMounted, ref } from "vue";
import { gameApi } from "@/entities/game";
import type { Post } from "@/entities/game";
import { GamePost } from "@/widgets/game-post";
import { GamePostSkeleton } from "@/shared/ui/Skeleton";
import { ErrorState } from "@/shared/ui/ErrorState";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";
import { useRoleGate } from "./lib/useRoleGate";

const { hasAccess } = useRoleGate("Moderator");

const posts = ref<Post[]>([]);
const loading = ref(false);
const loadError = ref<string | null>(null);

async function fetch() {
  loading.value = true;
  const { data, error } = await gameApi.getRatedPosts({
    hasReviews: true,
    sortBy: "lastreview",
    sortOrder: "desc",
    take: 50,
  });
  loading.value = false;
  if (error) {
    loadError.value = "Не удалось загрузить оцененные посты";
    return;
  }
  loadError.value = null;
  posts.value = data?.resources ?? [];
}

onMounted(fetch);

const isEmpty = computed(() => !loading.value && posts.value.length === 0);
</script>

<template>
  <div class="moderation-rated-posts">
    <page-title>Последние оцененные посты</page-title>

    <SecondaryText v-if="!hasAccess">
      Страница доступна только модераторам
    </SecondaryText>

    <ErrorState v-else-if="loadError" :message="loadError" :retry="fetch" />

    <GamePostSkeleton v-else-if="loading && !posts.length" :count="5" />

    <SecondaryText v-else-if="isEmpty">
      Оцененных постов пока нет
    </SecondaryText>

    <div v-else class="posts-list">
      <GamePost
        v-for="post in posts"
        :key="post.id"
        :post="post"
        show-navigation
      />
    </div>
  </div>
</template>

<style scoped lang="sass">
.posts-list
  display: flex
  flex-direction: column
  gap: $medium
</style>
