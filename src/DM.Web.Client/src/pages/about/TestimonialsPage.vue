<script setup lang="ts">
import { useRoute } from "vue-router";
import { useTestimonialStore } from "@/shared/stores/testimonials";
import { useFetchData } from "@/shared/lib/composables/useFetchData";
import { storeToRefs } from "pinia";
import Paging from "@/shared/ui/Paging/Paging.vue";
import { Testimonial } from "@/entities/testimonial";
import { ReviewsFilter, useReviewsFilter } from "@/features/review-filter";
import { CreateReviewForm } from "@/features/create-review";

const route = useRoute();
const testimonialStore = useTestimonialStore();
const { testimonials } = storeToRefs(testimonialStore);

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

  <p class="intro">
    Здесь собраны отзывы пользователей о DM.AM. Вы можете оставить свой отзыв ниже
    (только обычный текст, без форматирования).
  </p>

  <!-- Create Form (for authenticated users) -->
  <CreateReviewForm />

  <!-- Filter controls -->
  <ReviewsFilter />

  <div v-if="testimonials" class="testimonials-list">
    <!-- Top paging -->
    <template v-if="testimonials.paging && testimonials.paging.pages > 1">
      <div class="separator separator--paging">
        - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
        - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
        - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
        - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
        - - - - - - - - - - - - - -
      </div>
      <Paging
        :paging="testimonials.paging"
        :to="{ name: 'testimonials' }"
        :use-query="true"
      />
      <div class="separator separator--paging">
        - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
        - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
        - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
        - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
        - - - - - - - - - - - - - -
      </div>
    </template>

    <template
      v-for="(testimonial, index) in testimonials.resources"
      :key="testimonial.id"
    >
      <Testimonial
        :controls="true"
        :testimonial="testimonial"
        :search-query="filterState.search"
      />
      <div v-if="index < testimonials.resources.length - 1" class="separator">
        - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
        - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
        - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
        - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
        - - - - - - - - - - - - - -
      </div>
    </template>

    <!-- Paging between separators -->
    <template v-if="testimonials.paging && testimonials.paging.pages > 1">
      <div class="separator separator--paging">
        - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
        - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
        - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
        - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
        - - - - - - - - - - - - - -
      </div>
      <Paging
        :paging="testimonials.paging"
        :to="{ name: 'testimonials' }"
        :use-query="true"
      />
      <div class="separator separator--paging">
        - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
        - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
        - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
        - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
        - - - - - - - - - - - - - -
      </div>
    </template>
  </div>

  <secondary-text v-if="testimonials && testimonials.resources.length === 0">
    {{ hasActiveFilters ? "Отзывов по заданным фильтрам не найдено" : "Отзывов пока нет" }}
  </secondary-text>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Variables"
@import "src/assets/styles/Themes"

.intro
  margin-bottom: $medium
  color: $text
  line-height: 1.6

  a
    color: $link
    &:hover
      color: $link-hover

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

  &--paging
    margin: 0
</style>
