<script setup lang="ts">
/**
 * Green speech-bubble skeleton for testimonial loading states.
 *
 * Extracted from the duplicated markup/styles previously in
 * pages/about/TestimonialsPage.vue (.testimonials-skeleton) and
 * pages/home/RandomTestimonials.vue (.testimonial-skeleton) - bubble + 3
 * shimmer lines + footer author/date bars.
 *
 * Skeleton reserves space equal to one collapsed testimonial bubble
 * (3 lines of text @ 16px x 1.5 line-height = 72px of content +
 * 24px padding x 2 + ~24px for the author/date footer ~= 150px).
 * min-height formula below preserves that pixel budget so surrounding
 * content doesn't jump when the real testimonial(s) arrive.
 *
 * The bubble radius uses the shared $bubble-radius token (Variables.sass),
 * matching the real testimonial bubble.
 */
withDefaults(
  defineProps<{
    /** Number of skeleton bubbles to render */
    count?: number;
  }>(),
  { count: 1 },
);
</script>

<template>
  <div class="testimonial-skeleton-list" aria-hidden="true">
    <div v-for="n in count" :key="n" class="skeleton-item">
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
</template>

<style scoped lang="sass">
@import "src/assets/styles/Skeleton"

.testimonial-skeleton-list
  display: flex
  flex-direction: column
  gap: $medium

// Skeleton bubbles mirror the collapsed testimonial bubble shape
// (same padding/radius/min-height as the real .testimonial-text,
// see entities/testimonial TestimonialCard.vue).
.skeleton-bubble
  display: flex
  flex-direction: column
  justify-content: center
  gap: 8px
  padding: $medium + $tiny $medium + $small
  margin-bottom: $small
  border-radius: $bubble-radius
  background-color: $bg-highlight-green
  min-height: calc(1.5 * 3 * 1em + ($medium + $tiny) + 26px)

// Uses +skeleton-shimmer for animation/border-radius, then overrides
// the gradient for the green bubble context: lines are derived from
// $text-on-green (the bubble's own text color) instead of hardcoded
// white, so they stay visible in both light and dark themes.
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
