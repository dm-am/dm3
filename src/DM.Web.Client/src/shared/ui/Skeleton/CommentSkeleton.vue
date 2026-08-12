<script setup lang="ts">
/**
 * Skeleton for Comment placeholders (forum and game comments).
 *
 * Layout contract — matches CommentItem.vue:
 *   - Full layout: flex row — avatar 72×72 left + body right
 *   - Compact layout: block — no avatar, just body
 *   - Body: author name + date header, 3 content lines, footer meta
 *   - Card: $medium padding, 1px dashed $border, $bg-element
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
      <div v-if="!isCompactLayout" class="skeleton-avatar" />
      <div class="skeleton-body">
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
    </div>
  </div>
</template>

<style scoped lang="sass">
@import "@/assets/styles/Skeleton"

// Inter-card gap matches the real forum comments list ($small); the game
// comments list packs them flush (gap 0) via its dashed-border handoff.
.comment-skeleton-list
  display: flex
  flex-direction: column
  gap: $small

// Matches CommentItem.vue: flex row, $medium padding/gap, dashed border
.skeleton-comment
  display: flex
  gap: $medium
  padding: $medium
  border: 1px dashed $border
  background-color: $bg-element

  &.compact
    display: block

.skeleton-avatar
  flex-shrink: 0
  width: 72px
  height: 72px
  +skeleton-shimmer

.skeleton-body
  flex: 1
  min-width: 0
  display: flex
  flex-direction: column
  gap: $small

// Full-header geometry: author name (16px) stacked ABOVE the meta line
// (12px), matching CommentItem.vue's author block — a row would push the body
// text down when the real header replaces it.
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
// the literal stays. It promises nothing about the real body: .comment-text
// runs at line-height 1.6 on the 16px page font, so the three lines these 54px
// stand in for measure 76.8px, and the card resizes at the handoff whatever
// this gap is.
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
