<script setup lang="ts">
/**
 * Skeleton for GamePost (featured mode) placeholders. Used by
 * BestWeeklyPost, LatestRatedPost, and ProfileBestPost while the real
 * data is fetching.
 *
 * Layout contract — must match the featured-mode <GamePost> shape:
 *   1. A breadcrumb row (Game > Room) — ~20px tall including margin.
 *   2. A post-card with two columns:
 *        - left post-meta: 160px wide, contains character name, avatar,
 *          date, rating.
 *        - right post-body: truncated content clamped to maxHeight
 *          (default 150px).
 *   3. Card has 5px padding + 1px dashed border.
 *
 * The combined natural height of one featured post is ~200-220px; the
 * skeleton reserves the same space so the block below stays put when
 * the real data arrives.
 */
</script>

<template>
  <div class="game-post-skeleton" aria-hidden="true">
    <!-- Navigation breadcrumb (Game > Room) -->
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

        <!-- Right column: post-body, clamped to 150px (maxHeight default) -->
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

.game-post-skeleton
  display: flex
  flex-direction: column

// Breadcrumb: matches .post-nav (font-size 16 × line-height 1.3 = 21px
// line, margin-bottom $small = 8px). Earlier values (18px + 4px) were
// 7px too short and produced a visible shift when the real breadcrumb
// materialized.
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
.skeleton-avatar
  +skeleton-shimmer
  width: 150px
  height: 150px
  margin-top: 4px

.skeleton-date
  +skeleton-shimmer
  width: 110px
  height: 13px
  margin-top: 4px

.skeleton-rating
  +skeleton-shimmer
  width: 80px
  height: 16px

// Right column: ~150px tall body (matches default maxHeight truncation).
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
