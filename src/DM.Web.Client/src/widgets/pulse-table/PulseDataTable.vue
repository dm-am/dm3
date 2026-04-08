<script setup lang="ts">
import { computed, watch } from "vue";
import { storeToRefs } from "pinia";
import Paging from "@/shared/ui/Paging/Paging.vue";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";
import { GamePost } from "@/pages/game";
import { usePulseStore } from "@/entities/game";
import { PulseFilter, usePulseFilter } from "@/features/pulse-filter";

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
</script>

<template>
  <div class="pulse-posts">
    <!-- Filters -->
    <PulseFilter class="filters" />

    <!-- Error state -->
    <div v-if="error" class="error-message">
      {{ error }}
    </div>

    <!-- Loading state -->
    <div v-else-if="loading" class="loading">
      <SecondaryText>Загрузка...</SecondaryText>
    </div>

    <!-- Empty state -->
    <SecondaryText v-else-if="posts.length === 0">
      {{ emptyText }}
    </SecondaryText>

    <!-- Posts list -->
    <template v-else>
      <div class="posts-list">
        <GamePost
          v-for="post in posts"
          :key="post.id"
          :post="post"
          show-navigation
        />
      </div>

      <!-- Pagination -->
      <Paging
        v-if="paging && paging.pages > 1"
        :paging="paging"
        :to="{ name: 'pulse' }"
        :use-query="true"
        class="pagination"
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

.loading
  padding: $medium 0

.posts-list
  display: flex
  flex-direction: column
  gap: $medium

.pagination
  margin-top: $medium
</style>
