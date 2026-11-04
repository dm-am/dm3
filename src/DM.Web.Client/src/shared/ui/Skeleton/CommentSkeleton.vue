<script setup lang="ts">
// The numbers copy CommentItem and CommentAuthor; CommentSkeleton.spec.ts holds them together.
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

// The full layout is the card of a post.
.skeleton-comment:not(.compact)
  padding: 5px

.skeleton-columns
  display: flex
  gap: 10px
  align-items: flex-start

// 140 wide, 128 of content; the strips stand where the lines of a two-line name do.
.skeleton-meta
  flex-shrink: 0
  box-sizing: border-box
  width: 140px
  padding: 11px 7px 11px 5px
  display: flex
  flex-direction: column
  gap: $minor

.skeleton-name
  width: 100px
  height: 16px
  +skeleton-shimmer

.skeleton-presence
  width: 64px
  height: 16px
  +skeleton-shimmer

// Never rounded.
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

// Where a post sets its text.
.skeleton-text
  flex: 1
  min-width: 0
  display: flex
  flex-direction: column
  gap: 6px
  padding: 22px 10px 0

// The number at the right of the card's padding.
.skeleton-number
  width: 16px
  height: 12px
  margin: 3px 0 0 auto
  +skeleton-shimmer

.skeleton-body
  display: flex
  flex-direction: column
  gap: $small

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

// A 20px pitch, the real body's: 14px of bar and this 6px, off the scale's steps.
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
