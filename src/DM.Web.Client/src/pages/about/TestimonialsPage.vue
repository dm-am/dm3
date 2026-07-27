<script setup lang="ts">
import { computed, ref } from "vue";
import { useRoute } from "vue-router";
import { useTestimonialStore } from "@/shared/stores/testimonials";
import { useFetchData } from "@/shared/lib/composables/useFetchData";
import { storeToRefs } from "pinia";
import PagingWithSeparators from "@/shared/ui/Paging/PagingWithSeparators.vue";
import LeadText from "@/shared/ui/Layout/LeadText.vue";
import { DashSeparator } from "@/shared/ui/DashSeparator";
import { ErrorState } from "@/shared/ui/ErrorState";
import { TestimonialCard, TestimonialSkeleton } from "@/entities/testimonial";
import { TestimonialDeleteButton } from "@/features/testimonial-delete";
import {
  TestimonialsFilter,
  useTestimonialsFilter,
} from "@/features/testimonial-filter";
import { CreateTestimonialForm } from "@/features/create-testimonial";
import { TESTIMONIALS_FORUM_TOPIC } from "@/shared/config/wellKnownRoutes";

const route = useRoute();
const testimonialStore = useTestimonialStore();
const { testimonials, error, loading } = storeToRefs(testimonialStore);

const { filterState, searchParams, hasActiveFilters, clearFilters } =
  useTestimonialsFilter();

useFetchData(
  () => testimonialStore.fetchTestimonials(searchParams.value),
  [
    {
      param: () => JSON.stringify(route.query),
      callback: () => testimonialStore.fetchTestimonials(searchParams.value),
    },
  ],
);

function retryFetch() {
  return testimonialStore.fetchTestimonials(searchParams.value, true);
}

// Out-of-range page: paging exists, current page has no resources, but
// earlier pages do (i.e. this isn't just an empty result set).
const currentPageOutOfRange = computed(() => {
  const list = testimonials.value;
  if (!list || !list.paging) return false;
  return list.resources.length === 0 && list.paging.number > 1;
});

// Paging scrolls the testimonials block (top separator + rows) back into
// view instead of the page top.
const listRef = ref<HTMLElement | null>(null);
function pagingAnchor(): HTMLElement | null {
  return listRef.value;
}
</script>

<template>
  <page-title v-once>Отзывы о сайте</page-title>

  <LeadText>
    Узнайте мнение игроков о Dungeon Master, а при желании поделитесь и
    собственным —
    <router-link :to="TESTIMONIALS_FORUM_TOPIC"
      >в топике с отзывами</router-link
    >
  </LeadText>

  <!-- Create Form owns its own moderator gate; render unconditionally -->
  <CreateTestimonialForm />

  <!-- Filter controls -->
  <TestimonialsFilter />

  <!-- Loading state (first load, no stale data yet): skeleton bubbles.
       Top-paging space is reserved so content doesn't jump when the
       real paging block appears above the list. -->
  <template v-if="!testimonials && !error">
    <div class="paging-space-reserve" aria-hidden="true" />
    <TestimonialSkeleton :count="3" />
  </template>

  <template v-else>
    <!-- Error banner: rendered above stale content when there is
         something to show, otherwise it is the only thing on screen. -->
    <ErrorState
      v-if="error"
      class="error-banner"
      :message="error"
      :retry="retryFetch"
    />

    <!-- Content (kept visible under the error banner during refetch) -->
    <div
      v-if="testimonials && testimonials.resources.length > 0"
      ref="listRef"
      class="testimonials-list"
      :class="{ 'is-refetching': loading }"
      :aria-busy="loading ? 'true' : undefined"
    >
      <!-- Top paging -->
      <PagingWithSeparators
        v-if="testimonials.paging"
        :paging="testimonials.paging"
        :to="{ name: 'testimonials' }"
        :use-query="true"
        :scroll-anchor="pagingAnchor"
      />

      <div class="testimonials-rows">
        <template
          v-for="(testimonial, index) in testimonials.resources"
          :key="testimonial.id"
        >
          <TestimonialCard
            :testimonial="testimonial"
            :search-query="filterState.search"
          >
            <template #controls>
              <TestimonialDeleteButton :testimonial="testimonial" />
            </template>
          </TestimonialCard>
          <DashSeparator
            v-if="index < testimonials.resources.length - 1"
            spacing="tiny"
          />
        </template>
      </div>

      <!-- Bottom paging -->
      <PagingWithSeparators
        v-if="testimonials.paging"
        :paging="testimonials.paging"
        :to="{ name: 'testimonials' }"
        :use-query="true"
        :scroll-anchor="pagingAnchor"
      />
    </div>

    <!-- Out-of-range page: paging exists but this page has no resources -->
    <div v-else-if="currentPageOutOfRange" class="empty-state">
      <secondary-text>
        На этой странице отзывов нет —
        <router-link :to="{ name: 'testimonials' }"
          >вернуться на первую страницу</router-link
        >
      </secondary-text>
    </div>

    <!-- Empty state -->
    <div v-else-if="testimonials" class="empty-state">
      <secondary-text>
        {{
          hasActiveFilters
            ? "Отзывов по заданным фильтрам не найдено"
            : "Отзывов пока нет"
        }}
      </secondary-text>
      <button
        v-if="hasActiveFilters"
        type="button"
        class="clear-filters-btn"
        @click="clearFilters"
      >
        Сбросить фильтры
      </button>
    </div>
  </template>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Inputs"

// Paging blocks sit at the same tight $tiny rhythm the rows keep between
// themselves and their dash separators (COMM-1: пагинация→строки =
// межстрочному, matching the polls page where both distances are equal).
.testimonials-list
  display: flex
  flex-direction: column
  gap: $tiny
  margin-top: $medium
  transition: opacity 0.15s ease

  // Dim feedback during a refetch (paging/sort/search) while stale
  // testimonials stay on screen.
  &.is-refetching
    opacity: 0.6
    pointer-events: none

.testimonials-rows
  display: flex
  flex-direction: column
  gap: $tiny

.error-banner
  margin-top: $medium

// Reserves the top PagingWithSeparators block's height during the
// skeleton state so paged results don't shift the list down once the
// real paging controls mount above it. Budget: two dash-separator
// lines (~21px each, no margins) + one .paging row (~22px links +
// 4px total vertical margin from the compact override) ~= 68px.
.paging-space-reserve
  height: 68px
  margin-top: $medium

// TestimonialSkeleton itself doesn't carry the page's top margin.
// No :deep() — the component's root element inherits this page's scope
// attribute, so the plain scoped selector matches it directly (a :deep
// descendant selector would not: the page is a root-level fragment).
// $tiny mirrors the loaded state's paging-to-rows gap so nothing shifts.
.testimonial-skeleton-list
  margin-top: $tiny

.empty-state
  display: flex
  align-items: center
  gap: $medium
  margin-top: $medium

.clear-filters-btn
  +inline-link-button
</style>
