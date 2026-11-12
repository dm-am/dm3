<script setup lang="ts">
// The bubble itself with shimmer in place of the text: its box is the real one, so nothing jumps when a testimonial arrives.
import { SpeechBubble } from "@/shared/ui/SpeechBubble";

withDefaults(
  defineProps<{
    count?: number;
  }>(),
  { count: 1 },
);
</script>

<template>
  <div class="testimonial-skeleton-list" aria-hidden="true">
    <SpeechBubble v-for="n in count" :key="n">
      <!-- Three lines: the height of a collapsed bubble. -->
      <span class="skeleton-text-row"
        ><span class="skeleton-text-line wide"
      /></span>
      <span class="skeleton-text-row"><span class="skeleton-text-line" /></span>
      <span class="skeleton-text-row"
        ><span class="skeleton-text-line short"
      /></span>
      <template #left><span class="skeleton-author" /></template>
      <template #right><span class="skeleton-date" /></template>
    </SpeechBubble>
  </div>
</template>

<style scoped lang="sass">
@use "@/assets/styles/Skeleton" as *

.testimonial-skeleton-list
  display: flex
  flex-direction: column
  gap: $medium

// One line of the bubble's text, at the bubble's own line height.
.skeleton-text-row
  display: flex
  align-items: center
  height: calc(var(--line-height, 1.5) * 1em)

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

// Inline in the footer's line, shorter than it, so the line keeps the height of the author's name.
.skeleton-author,
.skeleton-date
  display: inline-block
  vertical-align: middle
  height: 12px
  +skeleton-shimmer

.skeleton-author
  width: 120px

.skeleton-date
  width: 70px
</style>
