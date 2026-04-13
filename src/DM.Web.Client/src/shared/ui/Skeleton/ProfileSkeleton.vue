<script setup lang="ts">
/**
 * Skeleton loading for ProfileHeader.
 * Matches ProfileHeader dimensions:
 * - Avatar: 120×120px ($grid-step * 30)
 * - Gap: 32px ($big)
 * - Padding: 16px ($medium)
 * - Username height: 24px (1.5rem)
 */
</script>

<template>
  <div class="profile-skeleton" aria-hidden="true">
    <div class="skeleton-avatar" />
    <div class="skeleton-info">
      <div class="skeleton-name" />
      <div class="skeleton-status" />
      <div class="skeleton-stats">
        <div class="skeleton-stat" />
        <div class="skeleton-stat" />
      </div>
    </div>
  </div>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Variables"
@import "src/assets/styles/Themes"
@import "src/assets/styles/Skeleton"

.profile-skeleton
  display: flex
  gap: $big  // 32px - matches ProfileHeader
  padding: $medium  // 16px - matches ProfileHeader
  background: $bg-element
  border-radius: $border-radius  // 8px

.skeleton-avatar
  width: $grid-step * 30  // 120px - matches ProfileHeader
  height: $grid-step * 30
  border-radius: $border-radius
  flex-shrink: 0
  +skeleton-shimmer

.skeleton-info
  flex: 1
  display: flex
  flex-direction: column
  gap: $small  // 8px

.skeleton-name
  height: 24px  // matches 1.5rem
  width: 200px
  +skeleton-shimmer

.skeleton-status
  height: 16px  // matches body text
  width: 150px
  +skeleton-shimmer

.skeleton-stats
  display: flex
  flex-direction: column
  gap: $tiny  // 2px

.skeleton-stat
  height: 14px  // matches $secondary-font-size
  width: 180px
  +skeleton-shimmer
</style>
