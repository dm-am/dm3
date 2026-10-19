<script setup lang="ts">
import { computed, ref } from "vue";
import { useRoute } from "vue-router";
import { useTestimonialStore } from "@/entities/testimonial";
import { useFetchData } from "@/shared/lib/composables/useFetchData";
import { storeToRefs } from "pinia";
import PagingWithSeparators from "@/shared/ui/Paging/PagingWithSeparators.vue";
import LeadText from "@/shared/ui/Layout/LeadText.vue";
import { DashSeparator } from "@/shared/ui/DashSeparator";
import { ErrorState } from "@/shared/ui/ErrorState";
import { TestimonialSkeleton } from "@/entities/testimonial";
import { TestimonialEditable } from "@/features/testimonial-edit";
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

// A page past the end of a non-empty list, not an empty result.
const currentPageOutOfRange = computed(() => {
  const list = testimonials.value;
  if (!list || !list.paging) return false;
  return list.resources.length === 0 && list.paging.current > 1;
});

// Paging scrolls back to the list, not to the page top.
const listRef = ref<HTMLElement | null>(null);
</script>

<template>
  <page-title v-once>Отзывы о сайте</page-title>

  <LeadText>
    Узнайте мнение игроков о Dungeon Master, а при желании поделитесь и
    собственным:
    <router-link :to="TESTIMONIALS_FORUM_TOPIC"
      >в топике с отзывами</router-link
    >
  </LeadText>

  <!-- Create Form owns its own moderator gate; render unconditionally -->
  <CreateTestimonialForm />

  <TestimonialsFilter />

  <!-- The top paging space is reserved so the list does not jump when it loads. -->
  <template v-if="!testimonials && !error">
    <div class="paging-space-reserve" aria-hidden="true" />
    <TestimonialSkeleton :count="3" />
  </template>

  <template v-else>
    <!-- Above the stale list when there is one, alone otherwise. -->
    <ErrorState
      v-if="error"
      class="error-banner"
      :message="error"
      :retry="retryFetch"
    />

    <!-- Stays visible under the error banner during a refetch -->
    <div
      v-if="testimonials && testimonials.resources.length > 0"
      ref="listRef"
      class="testimonials-list"
      :class="{ 'is-refetching': loading }"
      :aria-busy="loading ? 'true' : undefined"
    >
      <PagingWithSeparators
        v-if="testimonials.paging"
        :paging="testimonials.paging"
        :to="{ name: 'testimonials' }"
        :use-query="true"
        :scroll-anchor="() => listRef"
      />

      <div class="testimonials-rows">
        <template
          v-for="(testimonial, index) in testimonials.resources"
          :key="testimonial.id"
        >
          <!-- The editor replaces the text in place, which a control in the bubble corner cannot do. -->
          <TestimonialEditable
            :testimonial="testimonial"
            :search-query="filterState.search"
          >
            <template #actions>
              <TestimonialDeleteButton :testimonial="testimonial" />
            </template>
          </TestimonialEditable>
          <DashSeparator
            v-if="index < testimonials.resources.length - 1"
            spacing="tiny"
          />
        </template>
      </div>

      <PagingWithSeparators
        v-if="testimonials.paging"
        :paging="testimonials.paging"
        :to="{ name: 'testimonials' }"
        :use-query="true"
        :scroll-anchor="() => listRef"
      />
    </div>

    <div v-else-if="currentPageOutOfRange" class="empty-state">
      <secondary-text>
        На этой странице отзывов нет.
        <router-link :to="{ name: 'testimonials' }"
          >Вернуться на первую страницу</router-link
        >
      </secondary-text>
    </div>

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
@use "@/assets/styles/Links" as *

// Paging keeps the $tiny gap the rows keep with their separators, as on the polls page.
.testimonials-list
  display: flex
  flex-direction: column
  gap: $tiny
  margin-top: $medium
  transition: opacity $transition-normal

  // Stale testimonials stay on screen, dimmed, during a refetch.
  &.is-refetching
    opacity: $opacity-half
    pointer-events: none

.testimonials-rows
  display: flex
  flex-direction: column
  gap: $tiny

.error-banner
  margin-top: $medium

// Holds the height of the top paging block during the skeleton, so the list does not shift.
.paging-space-reserve
  height: 68px
  margin-top: $medium

// The skeleton's root carries this page's scope attribute, so a plain scoped selector reaches it.
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
