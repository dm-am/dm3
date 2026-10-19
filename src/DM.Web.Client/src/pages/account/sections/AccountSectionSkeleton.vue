<script setup lang="ts">
/**
 * What a section of the account stands as while its first read is out: the
 * shape of what it will show, with no word in it. Each section answers with
 * one of three shapes, so the block does not jump when the answer arrives.
 *
 * - rows: the framed tiles of "Сессии", "Журнал безопасности", "Приглашения"
 *   and "Уведомления", a line of 16 and a caption line under it, $small $medium
 *   padding and $small between them.
 * - fields: a caption of 14 over a control of $control-height, at the width
 *   of a field of the account.
 * - checks: a box of $checkbox-size with its words, one line each.
 */
withDefaults(
  defineProps<{
    shape: "rows" | "fields" | "checks";
    /** How many of the shape stand. */
    count?: number;
  }>(),
  { count: 3 },
);
</script>

<template>
  <div
    class="account-skeleton"
    :class="`account-skeleton--${shape}`"
    aria-hidden="true"
  >
    <div v-for="i in count" :key="i" class="skeleton-item">
      <template v-if="shape === 'rows'">
        <div class="skeleton-line skeleton-line--title" />
        <div class="skeleton-line skeleton-line--caption" />
      </template>
      <template v-else-if="shape === 'fields'">
        <div class="skeleton-line skeleton-line--caption" />
        <div class="skeleton-control" />
      </template>
      <template v-else>
        <div class="skeleton-box" />
        <div class="skeleton-line skeleton-line--words" />
      </template>
    </div>
  </div>
</template>

<style scoped lang="sass">
@use "@/assets/styles/Skeleton" as *
@use "@/assets/styles/Inputs" as *
@use "../AccountPage.styles" as *

// The heights of the text the placeholders stand for: a line of the page and a
// caption line, each at the line height it is read at.
$title-line: 1.5 * $font-size
$caption-line: 1.5 * $secondary-font-size

.account-skeleton
  display: flex
  flex-direction: column
  gap: $small

// A tile as the rows of "Сессии" frame it.
.account-skeleton--rows .skeleton-item
  display: flex
  flex-direction: column
  gap: $tiny
  padding: $small $medium
  border: 1px solid $border

// A field as FormField lays it: the caption $minor over its control, $small round it.
.account-skeleton--fields .skeleton-item
  display: flex
  flex-direction: column
  gap: $minor
  margin: $small 0

.account-skeleton--checks .skeleton-item
  display: flex
  align-items: center
  gap: $small
  height: 1.3 * $font-size

.skeleton-line
  +skeleton-shimmer

.skeleton-line--title
  width: 40%
  height: $title-line

.skeleton-line--caption
  width: 30%
  height: $caption-line

.skeleton-line--words
  width: 50%
  height: $font-size

.skeleton-control
  width: 100%
  max-width: $account-field-width
  height: $control-height
  +skeleton-shimmer

.skeleton-box
  flex-shrink: 0
  width: $checkbox-size
  height: $checkbox-size
  +skeleton-shimmer
</style>
