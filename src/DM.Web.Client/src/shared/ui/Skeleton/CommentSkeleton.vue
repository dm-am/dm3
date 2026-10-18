<script setup lang="ts">
/**
 * Skeleton for Comment placeholders (forum and game comments).
 *
 * Layout contract — matches CommentItem.vue:
 *   - Full layout: the card of a post (GamePost.vue), 5px of padding around
 *     the author's column of 140, its content 128 (the name and its marks on
 *     two lines, avatar the width of the content, date, like), a spacer of 10 and the strips of the text,
 *     22px under the top of the column and 10px in from its edges; the number
 *     in the right corner under both. Beside each other at every width.
 *   - Compact layout: block — no avatar, just body: author name + date
 *     header, 3 content lines, footer meta; $medium padding
 *   - Card: 1px dashed $border, $bg-element
 */
import { storeToRefs } from "pinia";
import { useUiStore } from "@/shared/stores/ui";

const { isCompactLayout } = storeToRefs(useUiStore());

withDefaults(
  defineProps<{
    /** Number of skeleton comments to show */
    count?: number;
  }>(),
  { count: 5 },
);
</script>

<template>
  <div class="comment-skeleton-list" aria-hidden="true">
    <div
      v-for="i in count"
      :key="i"
      class="skeleton-comment"
      :class="{ compact: isCompactLayout }"
    >
      <div v-if="isCompactLayout" class="skeleton-body">
        <div class="skeleton-header">
          <div class="skeleton-author" />
          <div class="skeleton-date" />
        </div>
        <div class="skeleton-content">
          <div class="skeleton-line wide" />
          <div class="skeleton-line" />
          <div class="skeleton-line short" />
        </div>
        <div class="skeleton-footer" />
      </div>

      <template v-else>
        <div class="skeleton-columns">
          <!-- The author's column (.comment-header) -->
          <div class="skeleton-meta">
            <div class="skeleton-name" />
            <div class="skeleton-presence" />
            <div class="skeleton-avatar" />
            <div class="skeleton-when" />
            <div class="skeleton-like" />
          </div>

          <!-- The text (.comment-body) -->
          <div class="skeleton-text">
            <div class="skeleton-line wide" />
            <div class="skeleton-line" />
            <div class="skeleton-line short" />
          </div>
        </div>

        <!-- The number in the corner of the card (.comment-footer) -->
        <div class="skeleton-number" />
      </template>
    </div>
  </div>
</template>

<style scoped lang="sass">
@use "@/assets/styles/Skeleton" as *

// Inter-card gap matches the real forum comments list ($small); the game
// comments list packs them flush (gap 0) via its dashed-border handoff.
.comment-skeleton-list
  display: flex
  flex-direction: column
  gap: $small

// Matches CommentItem.vue, .comment: $medium padding, dashed border.
.skeleton-comment
  padding: $medium
  border: 1px dashed $border
  background-color: $bg-element

// The full layout is the card of a post (.comment:not(.compact)): 5px of
// padding around the columns and the row of the number.
.skeleton-comment:not(.compact)
  padding: 5px

// ── Full layout ──────────────────────────────────────────────────────────────

// The columns of a post (.comment-columns): the author's column, a spacer of
// 10 and the text beside it, at every width.
.skeleton-columns
  display: flex
  gap: 10px
  align-items: flex-start

// The author's column, 140 wide padding included (.comment-header), the
// padding being the .author-inner margin and its padding on the right, which
// leave 128 for the content. The strips stand where the lines do in the usual
// column, whose name and marks take two lines: the avatar 57 under the top of
// the column (11, two lines of 20 and their 4, the avatar's own 2), the bottom
// of the like 234.
.skeleton-meta
  flex-shrink: 0
  box-sizing: border-box
  width: 140px
  padding: 11px 7px 11px 5px
  display: flex
  flex-direction: column
  gap: 4px

.skeleton-name
  width: 100px
  height: 16px
  +skeleton-shimmer

.skeleton-presence
  width: 64px
  height: 16px
  +skeleton-shimmer

// Square, the width of the column's content, as the avatar fills it. Never
// rounded.
.skeleton-avatar
  width: 128px
  height: 128px
  margin-top: 6px
  +skeleton-shimmer($square: true)

.skeleton-when
  width: 110px
  height: 16px
  +skeleton-shimmer

.skeleton-like
  width: 32px
  height: 14px
  +skeleton-shimmer

// The text where a post sets it: .comment-body { padding: 11px 0 } and
// .comment-text { margin: 11px 10px }.
.skeleton-text
  flex: 1
  min-width: 0
  display: flex
  flex-direction: column
  gap: 6px
  padding: 22px 10px 0

// The number at the right of a row of 15 (.comment-footer), in the corner of
// the card's padding.
.skeleton-number
  width: 16px
  height: 12px
  margin: 3px 0 0 auto
  +skeleton-shimmer

// ── Compact layout ───────────────────────────────────────────────────────────

.skeleton-body
  display: flex
  flex-direction: column
  gap: $small

// The author name (16px) stacked ABOVE the meta line (12px).
.skeleton-header
  display: flex
  flex-direction: column
  align-items: flex-start
  gap: $tiny

.skeleton-author
  width: 120px
  height: 18px
  +skeleton-shimmer

.skeleton-date
  width: 90px
  height: 12px
  +skeleton-shimmer

// The bars repeat on a 20px pitch: 14px of bar plus this 6px, five steps of the
// 4px grid. The gap alone is on no step of the scale, and neither neighbouring
// step keeps the pitch on the grid ($minor gives 18px, $small gives 22px), so
// the literal stays. It is the pitch of the real body: .comment-text runs at
// line-height 1.25 on the 16px page font, so the three lines these 54px stand
// in for measure 60px on the same 20px step, the last of them a full line
// where its bar is 14.
.skeleton-content
  display: flex
  flex-direction: column
  gap: 6px

.skeleton-line
  height: 14px
  width: 100%
  +skeleton-shimmer

  &.wide
    width: 95%

  &.short
    width: 60%

.skeleton-footer
  width: 150px
  height: 12px
  margin-top: $small
  +skeleton-shimmer
</style>
