<script setup lang="ts">
import { computed, watch } from "vue";
import { storeToRefs } from "pinia";
import Paging from "@/shared/ui/Paging/Paging.vue";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";
import { GamePost } from "@/pages/game";
import { usePulseStore } from "@/entities/game";
import { PulseFilter, usePulseFilter } from "@/features/pulse-filter";
import PulsePostSkeleton from "./PulsePostSkeleton.vue";

const pulseStore = usePulseStore();
const { posts, paging, loading, error } = storeToRefs(pulseStore);

const { searchParams, hasActiveFilters } = usePulseFilter();

// Empty state text
const emptyText = computed(() =>
  hasActiveFilters.value
    ? "Постов по заданным фильтрам не найдено"
    : "Нет оцененных постов за эту неделю"
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
  { immediate: true }
);

const hasPaging = computed(() => paging.value && paging.value.pages > 1);
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
    <PulsePostSkeleton v-else-if="loading && posts.length === 0" :count="5" />

    <!-- Empty state -->
    <SecondaryText v-else-if="posts.length === 0">
      {{ emptyText }}
    </SecondaryText>

    <!-- Posts list -->
    <template v-else>
      <!-- Top paging with separators -->
      <template v-if="hasPaging">
        <div class="separator">
          - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
          - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
          - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
          - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
          - - - - - - - - - - - - - -
        </div>
        <Paging :paging="paging!" :to="{ name: 'pulse' }" :use-query="true" />
        <div class="separator">
          - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
          - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
          - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
          - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
          - - - - - - - - - - - - - -
        </div>
      </template>

      <div class="posts-list">
        <GamePost
          v-for="post in posts"
          :key="post.id"
          :post="post"
          show-navigation
        />
      </div>

      <!-- Bottom paging with separators -->
      <template v-if="hasPaging">
        <div class="separator">
          - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
          - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
          - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
          - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
          - - - - - - - - - - - - - -
        </div>
        <Paging :paging="paging!" :to="{ name: 'pulse' }" :use-query="true" />
        <div class="separator">
          - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
          - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
          - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
          - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
          - - - - - - - - - - - - - -
        </div>
      </template>
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

.separator
  margin: $tiny 0
  color: $text-muted
  white-space: nowrap
  overflow: hidden
  max-width: 100%
  width: 0
  min-width: 100%
  user-select: none
</style>
