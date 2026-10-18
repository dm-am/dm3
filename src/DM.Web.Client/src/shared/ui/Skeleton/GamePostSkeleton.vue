<script setup lang="ts">
/**
 * Skeleton for GamePost placeholders (featured and pulse modes).
 *
 * Layout contract — matches the real <GamePost> with show-navigation:
 *   1. Breadcrumb row (.post-nav) — OUTSIDE the card, ~21px tall.
 *   2. Post card (.post-card) with two columns, side by side at every width:
 *        - left post-meta: 140px wide, its content 128 (character, login,
 *          portrait, date, rating)
 *        - right post-body: truncated content lines, 22px under the top of
 *          the column and 10px in from its edges, where the text starts
 *   3. Card has 5px padding + 1px dashed border.
 *
 * When count > 1, renders multiple cards as far apart as the posts of the list
 * it stands in for: 16 in the feeds, whose posts carry the way to their room
 * (PulseFeed, RatedPostsList), and 20 in a room, whose posts stand without it,
 * as DM2 set the posts of a room (GameRoom.vue, count=3).
 * Single-post usage (count=1): BestWeeklyPost, LatestRatedPost, ProfileBestPost.
 */
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
      <!-- Navigation breadcrumb (Game > Room) — outside card, matching .post-nav -->
      <div v-if="showNavigation" class="skeleton-nav">
        <div class="skeleton-game" />
        <span class="nav-sep"> > </span>
        <div class="skeleton-room" />
      </div>

      <!-- Card matches .post-card (padding 5px, dashed border) -->
      <div class="skeleton-card">
        <div class="skeleton-columns">
          <!-- Left column: post-meta (width 140px) -->
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

        <!-- Footer meta (post number / anchor) — right-aligned, matching
             .post-footer, which is always present in the real card. -->
        <div class="skeleton-footer" />
      </div>
    </div>
  </div>
</template>

<style scoped lang="sass">
@use "@/assets/styles/Skeleton" as *

// The gap shows only when count > 1: the feeds' ($medium, .posts-list of
// PulseFeed and RatedPostsList).
.game-post-skeleton
  display: flex
  flex-direction: column
  gap: $medium

// The posts of a room, which stand without the way to it, 20 apart as on DM2
// (GameRoom.vue, .posts-list).
.game-post-skeleton.in-room
  gap: 5 * $grid-step

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

// Left column: 140px wide padding included (= .post-meta width), the padding
// being the .meta-inner margin and its padding on the right, which leave 128
// for the content.
.skeleton-meta
  flex-shrink: 0
  box-sizing: border-box
  width: 140px
  padding: 11px 7px 11px 5px
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

// Avatar: square, the width of the column's content (128px), as the portrait
// fills it, 57 under the top of the column where the portrait stands (11, the
// two lines of 20 of the character and the login, the portrait's own 2).
.skeleton-avatar
  +skeleton-shimmer($square: true)
  width: 128px
  height: 128px
  margin-top: 8px

.skeleton-date
  +skeleton-shimmer
  width: 110px
  height: 13px

.skeleton-rating
  +skeleton-shimmer
  width: 80px
  height: 16px

// Right column: body lines (matches default maxHeight truncation), where the
// text stands: .post-body { padding: 11px 0 } and .game-text { margin: 11px
// 10px }.
.skeleton-body
  flex: 1
  display: flex
  flex-direction: column
  gap: 8px
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
