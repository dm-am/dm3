<script setup lang="ts">
/**
 * Loading placeholder for the global chat message list — first load only.
 * Mirrors ChatMessage's full layout (72px avatar + header + content lines)
 * so nothing jumps once real messages replace it.
 */
withDefaults(
  defineProps<{
    /** Number of skeleton messages to show */
    count?: number;
  }>(),
  { count: 6 },
);
</script>

<template>
  <div class="chat-message-skeleton-list" aria-hidden="true">
    <div v-for="i in count" :key="i" class="skeleton-message">
      <div class="skeleton-avatar" />
      <div class="skeleton-body">
        <div class="skeleton-header">
          <div class="skeleton-author" />
          <div class="skeleton-time" />
        </div>
        <div class="skeleton-lines">
          <div class="skeleton-line wide" />
          <div class="skeleton-line short" />
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped lang="sass">
@import "@/assets/styles/Skeleton"

.chat-message-skeleton-list
  display: flex
  flex-direction: column
  gap: $medium

// Each message owns its vertical padding, like .globalChat-message
// ($small $medium) — without it every row is ~16px shorter than the real
// message and the loaded chat collapses upward.
.skeleton-message
  display: flex
  gap: $medium
  padding: $small $medium

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
  // Header sits tight to the text (msg-header margin-bottom $tiny), not
  // a full $small away.
  gap: $tiny

.skeleton-header
  display: flex
  align-items: center
  gap: $small

.skeleton-author
  width: 120px
  height: 14px
  +skeleton-shimmer

.skeleton-time
  width: 60px
  height: 12px
  +skeleton-shimmer

.skeleton-lines
  display: flex
  flex-direction: column
  gap: 6px

.skeleton-line
  height: 14px
  width: 100%
  +skeleton-shimmer

  &.wide
    width: 90%

  &.short
    width: 45%
</style>
