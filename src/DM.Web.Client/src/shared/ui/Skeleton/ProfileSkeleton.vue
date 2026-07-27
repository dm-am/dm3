<script setup lang="ts">
/**
 * Skeleton loading for ProfilePage — mirrors the real profile layout
 * pixel-for-pixel so nothing jumps once the profile loads:
 * - Title bar: single PageTitle-height bar ("Личный кабинет: username")
 * - Avatar: 220x220px (matches `.avatar-wrapper` in ProfilePage.vue)
 * - Stat lines: 7 short bars stacked vertically (registration, rating,
 *   given reviews, posts written, last activity, 2 endorsement lines)
 *
 * Flat — no card background, no border-radius, same as the loaded page
 * (ProfilePage's `.profile-page` has no card chrome either).
 */
</script>

<template>
  <div class="profile-skeleton" aria-hidden="true">
    <div class="skeleton-title" />

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
    </div>
  </div>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Skeleton"

.profile-skeleton
  display: flex
  flex-direction: column
  gap: $small

.skeleton-title
  width: 320px
  max-width: 100%
  height: 24px  // matches PageTitle (20px font + line-height)
  // PageTitle carries margin: $medium 0 $small — the top offset must be
  // mirrored or the whole page jumps up 16px when the real title loads.
  margin-top: $medium
  +skeleton-shimmer

.skeleton-identity
  display: flex
  flex-direction: column
  align-items: flex-start
  gap: $small

.skeleton-avatar
  width: 220px  // matches .avatar-wrapper in ProfilePage.vue
  height: 220px
  max-width: 100%
  // .avatar-wrapper has margin-bottom $small (on top of the column gap).
  margin-bottom: $small
  +skeleton-shimmer

// No role bar: the real role line renders only for staff (v-if on roles),
// so a permanent bar makes the skeleton 24px too tall for ordinary users.
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
