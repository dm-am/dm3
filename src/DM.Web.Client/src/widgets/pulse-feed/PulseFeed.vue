<script setup lang="ts">
import { computed, ref, watch } from "vue";
import { storeToRefs } from "pinia";
import PagingWithSeparators from "@/shared/ui/Paging/PagingWithSeparators.vue";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";
import { ErrorState } from "@/shared/ui/ErrorState";
import { GamePost } from "@/widgets/game-post/@x/pulse-feed";
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

function retryFetch() {
  return pulseStore.fetchPosts(searchParams.value);
}

// Paging scrolls the posts list back into view (not the page top)
const listRef = ref<HTMLElement | null>(null);
function pagingAnchor(): HTMLElement | null {
  return listRef.value;
}
</script>

<template>
  <div class="pulse-posts">
    <!-- Filters -->
    <PulseFilter class="filters" />

    <!-- Error state — independent banner, does not replace stale posts -->
    <ErrorState
      v-if="error"
      class="error-banner"
      :message="error"
      :retry="retryFetch"
    />

    <!-- Loading state — show skeleton only on the initial load, not on
         refetches. Keeping rendered posts visible during a background
         refetch avoids a jarring full-page blank flash when the user
         changes sort/filter. This mirrors the stale-while-revalidate
         pattern documented in PERFORMANCE.md. -->
    <GamePostSkeleton v-if="loading && posts.length === 0" :count="5" />

    <!-- Empty state -->
    <SecondaryText v-else-if="!error && posts.length === 0">
      {{ emptyText }}
    </SecondaryText>

    <!-- Posts list — kept visible under the error banner on a failed refetch -->
    <template v-else>
      <!-- Top paging with separators -->
      <PagingWithSeparators
        v-if="paging"
        :paging="paging"
        :to="{ name: 'pulse' }"
        :use-query="true"
        :scroll-anchor="pagingAnchor"
      />

      <div
        ref="listRef"
        class="posts-list"
        :class="{ 'with-paging': paging && paging.pages > 1 }"
      >
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
        :scroll-anchor="pagingAnchor"
      />
    </template>
  </div>
</template>

<style scoped lang="sass">
.pulse-posts
  width: 100%

.filters
  margin-bottom: $medium

.error-banner
  margin-bottom: $medium

.posts-list
  display: flex
  flex-direction: column
  gap: $medium

  // $medium between the paging blocks and the posts — same rhythm as
  // between the posts themselves (block flow, margins collapse to exactly
  // it). Only when paging is actually rendered, so a single-page list
  // doesn't grow dead space at the container edges.
  &.with-paging
    margin: $medium 0
</style>
