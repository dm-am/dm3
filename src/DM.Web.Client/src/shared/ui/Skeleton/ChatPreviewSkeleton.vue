<script setup lang="ts">
/**
 * Loading twin of ChatPreview for the conversations list.
 *
 * Geometry contract, matched to ChatPreview: a 48px avatar on the left, the
 * name/date row and the message preview on the right, $medium padding on a
 * $bg-element card, and the list's own $tiny gap between them. Without it the
 * page answered "Нет переписок" for the length of the request and then shoved
 * the answer in underneath.
 */
withDefaults(
  defineProps<{
    /** Number of skeleton rows to show */
    count?: number;
  }>(),
  { count: 5 },
);
</script>

<template>
  <div class="chat-skeleton-list" aria-hidden="true">
    <div v-for="i in count" :key="i" class="skeleton-chat">
      <div class="skeleton-avatar" />
      <div class="skeleton-body">
        <div class="skeleton-header">
          <div class="skeleton-name" />
          <div class="skeleton-date" />
        </div>
        <div class="skeleton-preview" />
      </div>
    </div>
  </div>
</template>

<style scoped lang="sass">
@use "@/assets/styles/Skeleton" as *

.chat-skeleton-list
  display: flex
  flex-direction: column
  gap: $tiny

.skeleton-chat
  display: flex
  align-items: center
  gap: $medium
  padding: $medium
  border-radius: $border-radius
  background-color: $bg-element

.skeleton-avatar
  flex-shrink: 0
  width: $grid-step * 12
  height: $grid-step * 12
  +skeleton-shimmer

.skeleton-body
  flex: 1
  min-width: 0

.skeleton-header
  display: flex
  justify-content: space-between
  align-items: baseline
  margin-bottom: $tiny

.skeleton-name
  width: 140px
  height: 16px
  +skeleton-shimmer

.skeleton-date
  width: 110px
  height: 12px
  +skeleton-shimmer

.skeleton-preview
  width: 70%
  height: 12px
  +skeleton-shimmer
</style>
