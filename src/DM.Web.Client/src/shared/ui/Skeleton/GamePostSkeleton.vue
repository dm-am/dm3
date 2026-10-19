<script setup lang="ts">
// The numbers copy GamePost; several cards stand as far apart as the posts of the list they stand in for.
withDefaults(
  defineProps<{
    /** Number of skeleton post cards to render */
    count?: number;
    /** Show navigation breadcrumb (Game > Room) above card */
    showNavigation?: boolean;
  }>(),
  { count: 1, showNavigation: true },
);
</script>

<template>
  <div
    class="game-post-skeleton"
    :class="{ 'in-room': !showNavigation }"
    aria-hidden="true"
  >
    <div v-for="i in count" :key="i" class="skeleton-post">
      <div v-if="showNavigation" class="skeleton-nav">
        <div class="skeleton-game" />
        <span class="nav-sep"> > </span>
        <div class="skeleton-room" />
      </div>

      <div class="skeleton-card">
        <div class="skeleton-columns">
          <div class="skeleton-meta">
            <div class="skeleton-character" />
            <div class="skeleton-username" />
            <div class="skeleton-avatar" />
            <div class="skeleton-date" />
            <div class="skeleton-rating" />
          </div>

          <!-- Right column: post-body -->
          <div class="skeleton-body">
            <div class="skeleton-line" />
            <div class="skeleton-line" />
            <div class="skeleton-line short" />
            <div class="skeleton-line" />
            <div class="skeleton-line long" />
            <div class="skeleton-line short" />
          </div>
        </div>

        <div class="skeleton-footer" />
      </div>
    </div>
  </div>
</template>

<style scoped lang="sass">
@use "@/assets/styles/Skeleton" as *

// The feeds' gap; it shows only when count > 1.
.game-post-skeleton
  display: flex
  flex-direction: column
  gap: $medium

// A room's posts stand 20 apart, as on DM2.
.game-post-skeleton.in-room
  gap: 5 * $grid-step

// The 21px line of .post-nav and its margin.
.skeleton-nav
  display: flex
  align-items: center
  gap: $tiny
  margin-bottom: $small
  height: 21px

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

// Card: matches .post-card (padding 5px + dashed border 1px).
.skeleton-card
  padding: 5px
  background-color: $bg-element
  border: 1px dashed $border

.skeleton-columns
  display: flex
  gap: 10px
  align-items: flex-start

// 140 wide, 128 of content.
.skeleton-meta
  flex-shrink: 0
  box-sizing: border-box
  width: 140px
  padding: 11px 7px 11px 5px
  display: flex
  flex-direction: column
  gap: $minor

.skeleton-character
  +skeleton-shimmer
  width: 120px
  height: 16px

.skeleton-username
  +skeleton-shimmer
  width: 95px
  height: 14px

// Square, where the portrait stands.
.skeleton-avatar
  +skeleton-shimmer($square: true)
  width: 128px
  height: 128px
  margin-top: $small

.skeleton-date
  +skeleton-shimmer
  width: 110px
  height: 13px

.skeleton-rating
  +skeleton-shimmer
  width: 80px
  height: 16px

// Where the post sets its text.
.skeleton-body
  flex: 1
  display: flex
  flex-direction: column
  gap: $small
  padding: 22px 10px 0

.skeleton-line
  +skeleton-shimmer
  height: 14px
  width: 100%

  &.short
    width: 70%

  &.long
    width: 90%

// Footer meta placeholder — right-aligned post number/anchor.
.skeleton-footer
  +skeleton-shimmer
  width: 48px
  height: 13px
  margin-top: $small
  margin-left: auto
</style>
