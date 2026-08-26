<script setup lang="ts">
import { computed, ref } from "vue";
import { storeToRefs } from "pinia";
import PagingWithSeparators from "@/shared/ui/Paging/PagingWithSeparators.vue";
import { PollCard } from "@/widgets/sidebar";
import { ErrorState } from "@/shared/ui/ErrorState";
import { usePollsStore } from "@/entities/poll";
import { usePollsFilter } from "@/features/poll-filter";
import { usePaging } from "@/shared/lib/composables/usePaging";

const pollsStore = usePollsStore();
const { polls, pollsLoading, pollsError } = storeToRefs(pollsStore);

// Skeleton grid mirrors the loaded page size (stale count when reloading)
const { pollsPerPage } = usePaging();
const skeletonCount = computed(
  () => polls.value?.resources.length || pollsPerPage.value,
);

// Two-state empty text
const { filterState, searchParams, hasActiveFilters } = usePollsFilter();
const emptyTitle = computed(() =>
  hasActiveFilters.value
    ? "Опросов по заданным фильтрам не найдено"
    : "Опросов пока нет",
);

// Out-of-range page: paging exists, current page has no resources, but
// earlier pages do (i.e. this isn't just an empty result set).
const currentPageOutOfRange = computed(() => {
  const list = polls.value;
  if (!list || !list.paging) return false;
  return list.resources.length === 0 && list.paging.current > 1;
});

function retry() {
  pollsStore.fetchPolls(searchParams.value);
}

// Paging scrolls the polls block (top separator + grid) back into view
// instead of the page top. The loading and loaded branches render
// different `.polls-list` wrappers — mutually exclusive, so one ref
// always points at the rendered one.
const listRef = ref<HTMLElement | null>(null);
function pagingAnchor(): HTMLElement | null {
  return listRef.value;
}
</script>

<template>
  <!-- Loading state: skeleton grid; paging stays visible when stale data is present -->
  <div v-if="pollsLoading" ref="listRef" class="polls-list" aria-busy="true">
    <PagingWithSeparators
      v-if="polls?.paging"
      :paging="polls.paging"
      :to="{ name: 'polls' }"
      :use-query="true"
      :scroll-anchor="pagingAnchor"
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
      :scroll-anchor="pagingAnchor"
    />
  </div>

  <!-- Error state -->
  <ErrorState v-else-if="pollsError" :message="pollsError" :retry="retry" />

  <!-- Out-of-range page: paging exists but this page has no resources -->
  <div v-else-if="currentPageOutOfRange" class="empty-state">
    <secondary-text>
      На этой странице опросов нет.
      <router-link :to="{ name: 'polls' }"
        >Вернуться на первую страницу</router-link
      >
    </secondary-text>
  </div>

  <!-- Empty state -->
  <div v-else-if="polls && polls.resources.length === 0" class="empty-state">
    <secondary-text>{{ emptyTitle }}</secondary-text>
  </div>

  <!-- Polls list -->
  <div v-else-if="polls" ref="listRef" class="polls-list">
    <!-- Top paging -->
    <PagingWithSeparators
      v-if="polls.paging"
      :paging="polls.paging"
      :to="{ name: 'polls' }"
      :use-query="true"
      :scroll-anchor="pagingAnchor"
    />

    <div class="polls-grid">
      <div v-for="poll in polls.resources" :key="poll.id" class="poll-card">
        <PollCard
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
      :scroll-anchor="pagingAnchor"
    />
  </div>
</template>

<style scoped lang="sass">
@use "@/assets/styles/Skeleton" as *

.polls-list
  display: flex
  flex-direction: column
  // Paging blocks sit $medium from the poll grid — same rhythm as between
  // the poll cards themselves (.polls-grid gap)
  gap: $medium
  margin-top: $medium

.polls-grid
  display: grid
  grid-template-columns: repeat(3, 1fr)
  gap: $medium

  @media (max-width: $bp-shell)
    grid-template-columns: repeat(2, 1fr)

  @media (max-width: $bp-mobile)
    grid-template-columns: 1fr

.poll-card
  padding: $medium
  border: 1px dashed $border
  background-color: $bg-element

  :deep(.poll)
    margin: 0

.empty-state
  margin-top: $medium

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
</style>
