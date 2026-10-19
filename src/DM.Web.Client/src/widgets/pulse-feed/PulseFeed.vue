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
import { stableCacheKey } from "@/shared/lib/utils/keyedCache";

const pulseStore = usePulseStore();
const { posts, paging, loading, error } = storeToRefs(pulseStore);

const { filterState, searchParams, hasActiveFilters } = usePulseFilter();

const emptyText = computed(() =>
  hasActiveFilters.value
    ? "Постов по заданным фильтрам не найдено"
    : "Нет оцененных постов за эту неделю",
);

const paramsKey = computed(() => stableCacheKey(searchParams.value));

watch(
  paramsKey,
  () => {
    pulseStore.fetchPosts(searchParams.value);
  },
  { immediate: true },
);

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
    <PulseFilter class="filters" />

    <!-- A banner of its own: it does not replace the posts already shown -->
    <ErrorState
      v-if="error"
      class="error-banner"
      :message="error"
      :retry="retryFetch"
    />

    <!-- The skeleton on the first load only: a refetch keeps the posts shown -->
    <GamePostSkeleton v-if="loading && posts.length === 0" :count="5" />

    <SecondaryText v-else-if="!error && posts.length === 0">
      {{ emptyText }}
    </SecondaryText>

    <template v-else>
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

      <PagingWithSeparators
        v-if="paging"
        :paging="paging"
        :to="{ name: 'pulse' }"
        :use-query="true"
        :on-prefetch="pulseStore.prefetchPage"
        :scroll-anchor="pagingAnchor"
      />
    </template>
  </div>
</template>

<style scoped lang="sass">
.pulse-posts
  width: 100%

// $medium + $small under the filter and the banner, as the shared list of rated posts (RatedPostsList) spaces them.
.filters
  margin-bottom: $medium + $small

.error-banner
  margin-bottom: $medium + $small

.posts-list
  display: flex
  flex-direction: column
  gap: $medium

  // $medium to the paging blocks, as between the posts; only when they are drawn.
  &.with-paging
    margin: $medium 0
</style>
