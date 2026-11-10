<script setup lang="ts">
// One twin for the warning and the ban lists, which draw on the same parts.
withDefaults(defineProps<{ count?: number }>(), { count: 1 });
</script>

<template>
  <div class="record-skeleton-list" aria-hidden="true">
    <div v-for="n in count" :key="n" class="record-skeleton">
      <div class="skeleton-block">
        <div class="skeleton-line skeleton-text"></div>
        <div class="skeleton-line skeleton-caption"></div>
      </div>
      <div class="skeleton-verdict">
        <div class="skeleton-line skeleton-verdict-text"></div>
        <div class="skeleton-line skeleton-caption"></div>
      </div>
    </div>
  </div>
</template>

<style scoped lang="sass">
@use "@/assets/styles/Skeleton" as *
@use "@/assets/styles/CommentParts" as *

// The steps of the real list, so it does not shift when the answer arrives.
.record-skeleton-list
  display: flex
  flex-direction: column
  gap: $medium
  margin-bottom: $medium

// RecordBlock's frame.
.skeleton-block
  +comment-frame

// The list of decisions and RecordVerdict together.
.skeleton-verdict
  padding: ($small + $minor) 0 $minor ($big + $small + $small)

.skeleton-line
  +skeleton-shimmer

.skeleton-text
  width: 90%
  height: 1.25em
  font-size: $font-size

  & + .skeleton-caption
    margin-top: $small

.skeleton-verdict-text
  width: 60%
  height: 1.4em
  font-size: $font-size
  margin-bottom: $small

.skeleton-caption
  width: 40%
  height: 1.4em
  font-size: $tertiary-font-size
</style>
