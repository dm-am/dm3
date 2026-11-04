<script setup lang="ts">
// The height of one collapsed bubble, so the content does not jump when it arrives.
withDefaults(
  defineProps<{
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
@use "@/assets/styles/Skeleton" as *

.testimonial-skeleton-list
  display: flex
  flex-direction: column
  gap: $medium

// The collapsed bubble of shared/ui/SpeechBubble.
.skeleton-bubble
  display: flex
  flex-direction: column
  justify-content: center
  gap: $small
  padding: $medium + $tiny $medium + $small
  margin-bottom: $small
  border-radius: $bubble-radius
  background-color: $bg-highlight-green
  min-height: calc(1.5 * 3 * 1em + ($medium + $tiny) + 26px)

// Derived from the bubble's text colour, so the lines show in both themes.
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
  // Clears the tail of the real bubble.
  margin-top: 18px

.skeleton-author
  width: 120px
  height: 14px
  +skeleton-shimmer

.skeleton-date
  width: 70px
  height: 12px
  +skeleton-shimmer
</style>
