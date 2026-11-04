<script setup lang="ts">
/**
 * Loading twin of the list of subscriptions: the intro over it, a section
 * title and the rows of its dash list, each a blank in the font of what will
 * stand there, so the lines are as tall as the real ones.
 */
import { BlockTitle, SecondaryText } from "@/shared/ui/Layout";

withDefaults(
  defineProps<{
    /** The rows of the section the real list is expected to have. */
    rows?: number;
  }>(),
  { rows: 3 },
);

// Ragged widths, as real names are.
const WIDTHS = ["45%", "35%", "55%", "40%", "50%"];
const widthOf = (row: number) => WIDTHS[(row - 1) % WIDTHS.length];
</script>

<template>
  <div class="subscription-list-skeleton" aria-hidden="true">
    <SecondaryText class="skeleton-intro"
      ><span class="skeleton-bar skeleton-bar--intro"
        >&nbsp;</span
      ></SecondaryText
    >
    <BlockTitle
      ><span class="skeleton-bar skeleton-bar--title">&nbsp;</span></BlockTitle
    >
    <ul class="skeleton-list">
      <li v-for="row in rows" :key="row">
        <span class="skeleton-bar" :style="{ width: widthOf(row) }"
          >&nbsp;</span
        >
      </li>
    </ul>
  </div>
</template>

<style scoped lang="sass">
@use "@/assets/styles/Skeleton" as *
@use "@/shared/ui/CaptionLine/dashList" as *

// The intro's margin and the row's font, as SubscriptionsPage draws them.
.skeleton-intro
  margin-bottom: $small

.skeleton-list
  +dash-list
  font-size: $font-size

.skeleton-bar
  display: inline-block
  +skeleton-shimmer

.skeleton-bar--intro
  width: 80%

.skeleton-bar--title
  width: $grid-step * 30
</style>
