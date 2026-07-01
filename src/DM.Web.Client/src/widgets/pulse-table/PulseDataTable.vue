<script setup lang="ts">
import { computed, watch } from "vue";
import { storeToRefs } from "pinia";
import PagingWithSeparators from "@/shared/ui/Paging/PagingWithSeparators.vue";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";
import { GamePost } from "@/widgets/game-post";
import { usePulseStore } from "@/entities/game";
import { PulseFilter, usePulseFilter } from "@/features/pulse-filter";
import { GamePostSkeleton } from "@/shared/ui/Skeleton";

const pulseStore = usePulseStore();
const { posts, paging, loading, error } = storeToRefs(pulseStore);

const { filterState, searchParams, hasActiveFilters } = usePulseFilter();

// Empty state text
const emptyText = computed(() =>
  hasActiveFilters.value
    ? "Постов по заданным фильтрам не найдено"
    : "Нет оцененных постов за эту неделю",
);

// Params key for deduplication
function createParamsKey(): string {
  return JSON.stringify(searchParams.value);
}

const paramsKey = computed(() => createParamsKey());

// Fetch on params change
watch(
  paramsKey,
  () => {
    pulseStore.fetchPosts(searchParams.value);
  },
  { immediate: true },
);

// Prefetch next page when pagination becomes visible
function handlePrefetch(page: number) {
  pulseStore.prefetchPage(page);
}
</script>

<template>
  <div class="pulse-posts">
    <!-- Filters -->
    <PulseFilter class="filters" />

    <!-- Error state -->
    <div v-if="error" class="error-message">
      {{ error }}
    </div>

    <!-- Loading state — show skeleton only on the initial load, not on
         refetches. Keeping rendered posts visible during a background
         refetch avoids a jarring full-page blank flash when the user
         changes sort/filter. This mirrors the stale-while-revalidate
         pattern documented in PERFORMANCE.md. -->
    <GamePostSkeleton v-else-if="loading && posts.length === 0" :count="5" />

    <!-- Empty state -->
    <SecondaryText v-else-if="posts.length === 0">
      {{ emptyText }}
    </SecondaryText>

    <!-- Posts list -->
    <template v-else>
      <!-- Top paging with separators -->
      <PagingWithSeparators
        v-if="paging"
        :paging="paging"
        :to="{ name: 'pulse' }"
        :use-query="true"
      />

      <div class="posts-list">
        <GamePost
          v-for="post in posts"
          :key="post.id"
          :post="post"
          show-navigation
          :search-query="filterState.search"
        />
      </div>

      <!-- Bottom paging with separators -->
      <PagingWithSeparators
        v-if="paging"
        :paging="paging"
        :to="{ name: 'pulse' }"
        :use-query="true"
        :on-prefetch="handlePrefetch"
      />
    </template>
  </div>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Themes"

.pulse-posts
  width: 100%

.filters
  margin-bottom: $medium

.error-message
  padding: $medium
  color: $text-on-red
  background-color: $bg-highlight-red
  border-radius: $border-radius
  margin-bottom: $medium

.posts-list
  display: flex
  flex-direction: column
  gap: $medium
</style>
