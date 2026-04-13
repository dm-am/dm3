<script setup lang="ts">
/**
 * Placeholder block for a full-width <GamePost> while Pulse data is
 * loading. Reserves a fixed slot per post so the subsequent blocks
 * (paging, filters) don't reflow when real posts arrive. See
 * PERFORMANCE.md → "Skeleton" convention.
 */
withDefaults(
  defineProps<{
    count?: number;
  }>(),
  { count: 5 },
);
</script>

<template>
  <div class="pulse-skeleton" aria-hidden="true">
    <div v-for="i in count" :key="i" class="skeleton-card">
      <div class="skeleton-nav">
        <div class="skeleton-game" />
        <span class="nav-sep"> > </span>
        <div class="skeleton-room" />
      </div>
      <div class="skeleton-columns">
        <div class="skeleton-meta">
          <div class="skeleton-character" />
          <div class="skeleton-username" />
          <div class="skeleton-avatar" />
          <div class="skeleton-date" />
          <div class="skeleton-rating" />
        </div>
        <div class="skeleton-body">
          <div class="skeleton-line" />
          <div class="skeleton-line" />
          <div class="skeleton-line short" />
          <div class="skeleton-line" />
          <div class="skeleton-line long" />
          <div class="skeleton-line short" />
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Variables"
@import "src/assets/styles/Themes"
@import "src/assets/styles/Skeleton"

.pulse-skeleton
  display: flex
  flex-direction: column
  gap: $medium

.skeleton-card
  padding: 5px
  background-color: $bg-element
  border: 1px dashed $border

.skeleton-nav
  display: flex
  align-items: center
  gap: $tiny
  margin-bottom: 4px
  height: 18px

.skeleton-game
  +skeleton-shimmer
  width: 180px
  height: 14px

.skeleton-room
  +skeleton-shimmer
  width: 120px
  height: 14px

.nav-sep
  color: $text-muted

.skeleton-columns
  display: flex
  gap: 10px
  align-items: flex-start

.skeleton-meta
  flex-shrink: 0
  width: 160px
  padding: 11px 5px
  display: flex
  flex-direction: column
  gap: 4px

.skeleton-character
  +skeleton-shimmer
  width: 120px
  height: 16px

.skeleton-username
  +skeleton-shimmer
  width: 95px
  height: 14px

.skeleton-avatar
  +skeleton-shimmer
  width: 150px
  height: 150px
  margin-top: 4px

.skeleton-date
  +skeleton-shimmer
  width: 110px
  height: 13px

.skeleton-rating
  +skeleton-shimmer
  width: 80px
  height: 16px

.skeleton-body
  flex: 1
  display: flex
  flex-direction: column
  gap: 8px
  padding-top: 4px

.skeleton-line
  +skeleton-shimmer
  height: 14px
  width: 100%

  &.short
    width: 70%

  &.long
    width: 90%
</style>
