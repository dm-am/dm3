<script setup lang="ts">
/**
 * Skeleton for GamePost placeholders (featured and pulse modes).
 *
 * Layout contract — matches the real <GamePost> with show-navigation:
 *   1. Breadcrumb row (.post-nav) — OUTSIDE the card, ~21px tall.
 *   2. Post card (.post-card) with two columns:
 *        - left post-meta: 160px wide (character, avatar, date, rating)
 *        - right post-body: truncated content lines
 *   3. Card has 5px padding + 1px dashed border.
 *
 * When count > 1 (pulse mode), renders multiple cards with $medium gap.
 * Single-post usage (count=1): BestWeeklyPost, LatestRatedPost, ProfileBestPost.
 * Multi-post usage (count=5): PulseDataTable.
 */
withDefaults(
  defineProps<{
    /** Number of skeleton post cards to render */
    count?: number;
  }>(),
  { count: 1 },
);
</script>

<template>
  <div class="game-post-skeleton" aria-hidden="true">
    <div v-for="i in count" :key="i" class="skeleton-post">
      <!-- Navigation breadcrumb (Game > Room) — outside card, matching .post-nav -->
      <div class="skeleton-nav">
        <div class="skeleton-game" />
        <span class="nav-sep"> > </span>
        <div class="skeleton-room" />
      </div>

      <!-- Card matches .post-card (padding 5px, dashed border) -->
      <div class="skeleton-card">
        <div class="skeleton-columns">
          <!-- Left column: post-meta (width 160px) -->
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
      </div>
    </div>
  </div>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Variables"
@import "src/assets/styles/Themes"
@import "src/assets/styles/Skeleton"

.game-post-skeleton
  display: flex
  flex-direction: column
  gap: $medium  // visible only when count > 1

// Breadcrumb: matches .post-nav (font-size 16 × line-height 1.3 = 21px
// line, margin-bottom $small = 8px).
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

// Left column: 160px wide (= .post-meta width), with .meta-inner margin.
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

// Avatar: square, constrained by meta column width (150px after padding).
// margin-top: 2px matches .post-avatar { margin: 2px 0 }.
.skeleton-avatar
  +skeleton-shimmer
  width: 150px
  height: 150px
  margin-top: 2px

.skeleton-date
  +skeleton-shimmer
  width: 110px
  height: 13px
  margin-top: 4px

.skeleton-rating
  +skeleton-shimmer
  width: 80px
  height: 16px

// Right column: body lines (matches default maxHeight truncation).
// padding-top: $small matches .post-body { padding-top: $small }.
.skeleton-body
  flex: 1
  display: flex
  flex-direction: column
  gap: 8px
  padding-top: $small

.skeleton-line
  +skeleton-shimmer
  height: 14px
  width: 100%

  &.short
    width: 70%

  &.long
    width: 90%
</style>
