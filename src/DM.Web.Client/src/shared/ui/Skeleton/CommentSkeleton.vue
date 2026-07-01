<script setup lang="ts">
/**
 * Skeleton for Comment placeholders (forum and game comments).
 *
 * Layout contract — matches Comment.vue:
 *   - Full layout: flex row — avatar 64×64 left + body right
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
@import "src/assets/styles/Variables"
@import "src/assets/styles/Themes"
@import "src/assets/styles/Skeleton"

.comment-skeleton-list
  display: flex
  flex-direction: column
  gap: $medium

// Matches Comment.vue: flex row, $medium padding/gap, dashed border
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
  width: 64px
  height: 64px
  +skeleton-shimmer

.skeleton-body
  flex: 1
  min-width: 0
  display: flex
  flex-direction: column
  gap: $small

.skeleton-header
  display: flex
  align-items: center
  gap: $small

.skeleton-author
  width: 120px
  height: 14px
  +skeleton-shimmer

.skeleton-date
  width: 90px
  height: 12px
  +skeleton-shimmer

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
  margin-top: $tiny
  +skeleton-shimmer
</style>
