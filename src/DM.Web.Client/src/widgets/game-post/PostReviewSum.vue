<script setup lang="ts">
/** The sum of a post's reviews: it opens them, or is plain text where nothing opens. */
import { computed } from "vue";
import type { PostReviews } from "./usePostReviews";

const props = defineProps<{ reviews: PostReviews }>();

const sumText = computed(() => {
  const sum = props.reviews.sum ?? 0;
  return sum >= 0 ? `+${sum}` : String(sum);
});
const sumTone = computed(() => {
  const sum = props.reviews.sum ?? 0;
  if (sum > 0) return "positive";
  if (sum < 0) return "negative";
  return "neutral";
});
</script>

<template>
  <span class="rating-line"
    >Оценки:
    <!--
  --><button
      v-if="reviews.canOpen"
      type="button"
      class="rating-value"
      :class="sumTone"
      :aria-expanded="reviews.open"
      :aria-controls="reviews.collapseId"
      :aria-label="`Оценки ${sumText}, развернуть`"
      @click="reviews.toggle"
    >
      <b>{{ sumText }}</b></button
    ><span v-else class="rating-value" :class="sumTone"
      ><b>{{ sumText }}</b></span
    >
  </span>
</template>

<style scoped lang="sass">
// "Оценки:" label in normal text color, value is colored
.rating-line
  color: $text
  font-size: $font-size

.rating-value
  &.positive
    color: $accent-green
  &.negative
    color: $accent-red
  &.neutral
    color: $text-muted

// A real <button>, reset to look like the text; the pointer adds an underline only.
button.rating-value
  display: inline
  margin: 0
  padding: 0
  border: none
  background: none
  font: inherit
  cursor: pointer
  text-decoration: none
  &:hover
    text-decoration: underline
</style>
