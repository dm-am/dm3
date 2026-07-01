<script setup lang="ts">
import { computed } from "vue";
import { storeToRefs } from "pinia";
import PagingWithSeparators from "@/shared/ui/Paging/PagingWithSeparators.vue";
import Poll from "@/widgets/sidebar/Poll.vue";
import { EmptyState } from "@/shared/ui/EmptyState";
import { usePollsStore } from "@/entities/poll";
import { PollsFilter, usePollsFilter } from "@/features/poll-filter";
import { CreatePollForm } from "@/features/create-poll";
import { usePaging } from "@/shared/lib/composables";
import LeadText from "@/shared/ui/Layout/LeadText.vue";

const pollsStore = usePollsStore();
const { polls, pollsLoading, pollsError } = storeToRefs(pollsStore);

// Skeleton grid mirrors the loaded page size (stale count when reloading)
const { pollsPerPage } = usePaging();
const skeletonCount = computed(
  () => polls.value?.resources.length || pollsPerPage.value,
);

// Two-state empty text
const { filterState, hasActiveFilters } = usePollsFilter();
const emptyTitle = computed(() =>
  hasActiveFilters.value
    ? "Опросов по заданным фильтрам не найдено"
    : "Опросов пока нет",
);
const emptyHint = computed(() =>
  hasActiveFilters.value ? "Попробуйте изменить параметры поиска" : undefined,
);
</script>

<template>
  <page-title>Опросы</page-title>
  <LeadText>Запланированные, текущие и завершенные опросы сообщества</LeadText>

  <!-- Create poll form (moderators only) -->
  <CreatePollForm />

  <!-- Filter -->
  <PollsFilter />

  <!-- Loading state: skeleton grid; paging stays visible when stale data is present -->
  <div v-if="pollsLoading" class="polls-list" aria-busy="true">
    <PagingWithSeparators
      v-if="polls?.paging"
      :paging="polls.paging"
      :to="{ name: 'polls' }"
      :use-query="true"
    />

    <div class="polls-grid" aria-hidden="true">
      <div v-for="n in skeletonCount" :key="n" class="poll-card">
        <div class="poll-skeleton">
          <div class="skeleton-line skeleton-title" />
          <div class="skeleton-line skeleton-status" />
          <div v-for="i in 3" :key="i" class="skeleton-option">
            <div class="skeleton-line skeleton-option-label" />
            <div class="skeleton-line skeleton-option-bar" />
          </div>
        </div>
      </div>
    </div>

    <PagingWithSeparators
      v-if="polls?.paging"
      :paging="polls.paging"
      :to="{ name: 'polls' }"
      :use-query="true"
    />
  </div>

  <!-- Error state -->
  <div v-else-if="pollsError" class="error-message">
    {{ pollsError }}
  </div>

  <!-- Empty state -->
  <EmptyState
    v-else-if="polls && polls.resources.length === 0"
    :title="emptyTitle"
    :hint="emptyHint"
  />

  <!-- Polls list -->
  <div v-else-if="polls" class="polls-list">
    <!-- Top paging -->
    <PagingWithSeparators
      v-if="polls.paging"
      :paging="polls.paging"
      :to="{ name: 'polls' }"
      :use-query="true"
    />

    <div class="polls-grid">
      <div v-for="poll in polls.resources" :key="poll.id" class="poll-card">
        <Poll
          :poll="poll"
          :controls="true"
          :search-query="filterState.search"
        />
      </div>
    </div>

    <!-- Bottom paging -->
    <PagingWithSeparators
      v-if="polls.paging"
      :paging="polls.paging"
      :to="{ name: 'polls' }"
      :use-query="true"
    />
  </div>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Variables"
@import "src/assets/styles/Themes"
@import "src/assets/styles/Skeleton"

.polls-list
  display: flex
  flex-direction: column
  gap: $tiny
  margin-top: $medium

.polls-grid
  display: grid
  grid-template-columns: repeat(3, 1fr)
  gap: $medium

  @media (max-width: 1000px)
    grid-template-columns: repeat(2, 1fr)

  @media (max-width: 600px)
    grid-template-columns: 1fr

.poll-card
  padding: $medium
  border: 1px solid $border
  border-radius: $border-radius
  background-color: $bg-element

  :deep(.poll)
    margin: 0

// Skeleton card mirrors the poll card content: title, status line,
// then options (label + progress bar). Uses the shared shimmer mixin.
.poll-skeleton
  display: flex
  flex-direction: column
  gap: $small

.skeleton-line
  height: 12px
  +skeleton-shimmer

.skeleton-title
  width: 70%
  height: 16px

.skeleton-status
  width: 50%

.skeleton-option
  display: flex
  flex-direction: column
  gap: $tiny

.skeleton-option-label
  width: 40%

.skeleton-option-bar
  width: 100%
  height: 16px

.error-message
  padding: $medium
  color: $text-on-red
  background-color: $bg-highlight-red
  border-radius: $border-radius
  margin-bottom: $medium
</style>
