<script setup lang="ts">
/**
 * The waiting twin of a ticket (TicketThread): the dashed rectangle with its
 * heading over a dashed rule, two lines of text and the caption, in the
 * geometry of the real block, so nothing moves when the ticket arrives.
 *
 * How many to draw is the caller's: a list waits on three, a page on one.
 */
withDefaults(
  defineProps<{
    /** Number of tickets to draw while the list loads */
    count?: number;
  }>(),
  { count: 1 },
);
</script>

<template>
  <div class="ticket-skeleton-list" aria-hidden="true">
    <div v-for="n in count" :key="n" class="ticket-skeleton">
      <div class="skeleton-title">
        <div class="skeleton-line skeleton-heading"></div>
      </div>
      <div class="skeleton-line skeleton-text"></div>
      <div class="skeleton-line skeleton-text-short"></div>
      <div class="skeleton-line skeleton-caption"></div>
    </div>
  </div>
</template>

<style scoped lang="sass">
@use "@/assets/styles/Skeleton" as *

// The column of a list of tickets, a medium step between two.
.ticket-skeleton-list
  display: flex
  flex-direction: column
  gap: $medium

// RecordBlock: the comment's frame, ground and padding.
.ticket-skeleton
  padding: $medium
  border: 1px dashed $border
  background-color: $bg-element

// RecordBlock .record-block__title: the heading's step and rule under it.
.skeleton-title
  margin-bottom: $small
  padding-bottom: $small
  border-bottom: 1px dashed $border

// A bar is the height of a letter, and with its margins the height of the
// line box it stands in for, so two lines of text read as two.
.skeleton-heading
  width: 40%
  height: 1em
  margin: 0.1em 0
  font-size: $font-size
  +skeleton-shimmer

.skeleton-text
  width: 95%
  height: 1em
  margin: 0.3em 0
  font-size: $font-size
  +skeleton-shimmer

.skeleton-text-short
  width: 70%
  height: 1em
  margin: 0.3em 0
  font-size: $font-size
  +skeleton-shimmer

.skeleton-caption
  width: 30%
  height: 1em
  margin-top: calc(#{$small} + 0.2em)
  font-size: $tertiary-font-size
  +skeleton-shimmer
</style>
