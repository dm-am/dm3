<script setup lang="ts">
import { computed } from "vue";
import { useRoute } from "vue-router";
import { useTestimonialStore } from "@/shared/stores/testimonials";
import { useFetchData } from "@/shared/lib/composables/useFetchData";
import { storeToRefs } from "pinia";
import PagingWithSeparators from "@/shared/ui/Paging/PagingWithSeparators.vue";
import LeadText from "@/shared/ui/Layout/LeadText.vue";
import { Testimonial } from "@/entities/testimonial";
import { ReviewsFilter, useReviewsFilter } from "@/features/review-filter";
import { CreateReviewForm } from "@/features/create-review";
import { useUserStore, UserRole } from "@/entities/user";

const route = useRoute();
const testimonialStore = useTestimonialStore();
const { testimonials, error } = storeToRefs(testimonialStore);

const userStore = useUserStore();
const { user } = storeToRefs(userStore);

// Testimonials are a moderator-only review barrier — regular users post in the
// forum topic, only moderators add entries here.
const isModerator = computed(
  () =>
    user.value?.roles?.some((r) =>
      [UserRole.Admin, UserRole.SeniorModerator, UserRole.Moderator].includes(
        r,
      ),
    ) ?? false,
);

const { filterState, searchParams, hasActiveFilters } = useReviewsFilter();

useFetchData(
  () => testimonialStore.fetchTestimonials(searchParams.value),
  [
    {
      param: () => JSON.stringify(route.query),
      callback: () => testimonialStore.fetchTestimonials(searchParams.value),
    },
  ],
);
</script>

<template>
  <page-title v-once>Отзывы о сайте</page-title>

  <LeadText>
    Здесь собраны отзывы игроков о DM.AM. Будем рады, если поделитесь и своим —
    <router-link to="/forum/general/1">в топике на форуме</router-link>
  </LeadText>

  <!-- Create Form (moderators only — testimonials are a moderator-curated review barrier) -->
  <CreateReviewForm v-if="isModerator" />

  <!-- Filter controls -->
  <ReviewsFilter />

  <!-- Loading state (first load): skeleton bubbles -->
  <div
    v-if="!testimonials && !error"
    class="testimonials-skeleton"
    aria-hidden="true"
  >
    <div v-for="n in 3" :key="n" class="skeleton-item">
      <div class="skeleton-bubble">
        <div class="skeleton-text-line wide" />
        <div class="skeleton-text-line" />
        <div class="skeleton-text-line short" />
      </div>
      <div class="skeleton-footer">
        <div class="skeleton-author" />
        <div class="skeleton-date" />
      </div>
    </div>
  </div>

  <!-- Error state -->
  <div v-else-if="error" class="error-message">
    {{ error }}
  </div>

  <!-- Content -->
  <div
    v-else-if="testimonials && testimonials.resources.length > 0"
    class="testimonials-list"
  >
    <!-- Top paging -->
    <PagingWithSeparators
      v-if="testimonials.paging"
      :paging="testimonials.paging"
      :to="{ name: 'testimonials' }"
      :use-query="true"
    />

    <template
      v-for="(testimonial, index) in testimonials.resources"
      :key="testimonial.id"
    >
      <Testimonial
        :controls="true"
        :testimonial="testimonial"
        :search-query="filterState.search"
      />
      <div
        v-if="index < testimonials.resources.length - 1"
        class="separator"
        aria-hidden="true"
      >
        - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
        - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
        - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
        - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
        - - - - - - - - - - - - - -
      </div>
    </template>

    <!-- Bottom paging -->
    <PagingWithSeparators
      v-if="testimonials.paging"
      :paging="testimonials.paging"
      :to="{ name: 'testimonials' }"
      :use-query="true"
    />
  </div>

  <!-- Empty state -->
  <secondary-text v-else-if="testimonials">
    {{
      hasActiveFilters
        ? "Отзывов по заданным фильтрам не найдено"
        : "Отзывов пока нет"
    }}
  </secondary-text>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Variables"
@import "src/assets/styles/Themes"
@import "src/assets/styles/Skeleton"

.testimonials-list
  display: flex
  flex-direction: column
  gap: $tiny
  margin-top: $medium

.separator
  margin: $tiny 0
  color: $text-muted
  white-space: nowrap
  overflow: hidden
  max-width: 100%
  width: 0
  min-width: 100%
  user-select: none

.error-message
  margin-top: $medium
  padding: $medium
  color: $text-on-red
  background-color: $bg-highlight-red
  border-radius: $border-radius

// Skeleton bubbles mirror the collapsed testimonial bubble shape
// (same padding/radius/min-height as the real .testimonial-text,
// see entities/testimonial Testimonial.vue and RandomTestimonials).
.testimonials-skeleton
  display: flex
  flex-direction: column
  gap: $medium
  margin-top: $medium

.skeleton-bubble
  display: flex
  flex-direction: column
  justify-content: center
  gap: 8px
  padding: $medium + $tiny $medium + $small
  margin-bottom: $small
  border-radius: 20px
  background-color: $bg-highlight-green
  min-height: calc(1.5 * 3 * 1em + ($medium + $tiny) + 26px)

// Uses +skeleton-shimmer for animation/border-radius, then overrides
// the gradient for the green bubble context: lines are derived from
// $text-on-green (the bubble's own text color) instead of hardcoded
// white, so they stay visible in both light and dark themes
// (same approach as RandomTestimonials on the home page).
.skeleton-text-line
  height: 14px
  width: 100%
  +skeleton-shimmer
  border-radius: 3px
  background: linear-gradient(90deg, color-mix(in srgb, $text-on-green 25%, transparent) 25%, color-mix(in srgb, $text-on-green 50%, transparent) 50%, color-mix(in srgb, $text-on-green 25%, transparent) 75%)
  background-size: 200% 100%

  &.wide
    width: 95%

  &.short
    width: 60%

.skeleton-footer
  display: flex
  align-items: center
  justify-content: space-between
  gap: $small
  margin-top: 18px // Clear the real bubble's tail position

.skeleton-author
  width: 120px
  height: 14px
  +skeleton-shimmer

.skeleton-date
  width: 70px
  height: 12px
  +skeleton-shimmer
</style>
