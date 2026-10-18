<script setup lang="ts">
/**
 * The waiting twin of a moderation record (RecordBlock, RecordVerdictList and
 * RecordVerdict): the dashed square with its text and caption, and the
 * bulleted decision with its caption, in the geometry of the real thing.
 *
 * A warning and a ban are drawn on the same parts, so the lists of both wait
 * on this one twin; a copy per list drifts the first time the record changes
 * shape. The square of a row with no text is the caption alone, and the
 * caller says so: the number of lines is the list's to know.
 *
 * How many records to draw is the caller's, the way every skeleton of a list
 * takes it: the number belongs to the list being waited for, not to the block.
 */
withDefaults(
  defineProps<{
    /** Number of records to draw while the list loads */
    count?: number;
    /** The square carries a line of text above its caption */
    withText?: boolean;
  }>(),
  { count: 1, withText: true },
);
</script>

<template>
  <div class="record-skeleton-list" aria-hidden="true">
    <div v-for="n in count" :key="n" class="record-skeleton">
      <div class="skeleton-block">
        <div v-if="withText" class="skeleton-line skeleton-text"></div>
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

// The same column and the same steps the real list keeps, so the list does not
// shift when the answer arrives.
.record-skeleton-list
  display: flex
  flex-direction: column
  gap: $medium
  margin-bottom: $medium

// RecordBlock: the comment's frame, ground and padding.
.skeleton-block
  padding: $medium
  border: 1px dashed $border
  background-color: $bg-element

// RecordVerdictList and RecordVerdict: the list's step down and its indent,
// the item's margin and its own indent.
.skeleton-verdict
  padding: ($small + $minor) 0 $minor ($big + $small + $small)

// A line is the height of the line box it stands in for.
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
