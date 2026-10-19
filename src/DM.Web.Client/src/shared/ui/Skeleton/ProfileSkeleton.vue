<script setup lang="ts">
/** Mirrors ProfilePage: the three groups of stat lines it draws, so the block does not grow on load. */
import PageTitleSkeleton from "./PageTitleSkeleton.vue";
</script>

<template>
  <div class="profile-skeleton" aria-hidden="true">
    <PageTitleSkeleton />

    <div class="skeleton-identity">
      <div class="skeleton-avatar" />
      <div class="skeleton-status" />

      <div class="skeleton-stats">
        <div class="skeleton-stat" />
        <div class="skeleton-stat" />
        <div class="skeleton-stat" />
        <div class="skeleton-stat" />
        <div class="skeleton-stat" />
      </div>
      <div class="skeleton-stats">
        <div class="skeleton-stat" />
        <div class="skeleton-stat" />
      </div>
      <div class="skeleton-stats">
        <div class="skeleton-stat" />
        <div class="skeleton-stat" />
      </div>
    </div>
  </div>
</template>

<style scoped lang="sass">
@use "@/assets/styles/Skeleton" as *

.profile-skeleton
  display: flex
  flex-direction: column
  gap: $small

.skeleton-identity
  display: flex
  flex-direction: column
  align-items: flex-start
  gap: $small

.skeleton-avatar
  width: 220px  // matches .avatar-wrapper in ProfilePage.vue
  // Square: the avatar's own size arrives with the profile.
  height: 220px
  max-width: 100%
  // .avatar-wrapper has margin-bottom $small (on top of the column gap).
  margin-bottom: $small
  +skeleton-shimmer($square: true)

// No role bar: the role line renders for staff alone.
.skeleton-status
  height: 24px  // matches .status-row line-height (1.5 x 16px)
  width: 180px
  +skeleton-shimmer

.skeleton-stats
  display: flex
  flex-direction: column
  gap: $minor
  width: 100%
  margin-top: $small

  // The endorsement group sits a notch further down (margin-top $medium).
  & + &
    margin-top: $medium

.skeleton-stat
  height: 20px  // matches StatLine row (line-height 1.25 x 16px)
  width: 200px
  max-width: 100%
  +skeleton-shimmer
</style>
