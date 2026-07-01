<script setup lang="ts">
import { computed } from "vue";

const props = defineProps<{
  rating: number | null | undefined;
}>();

const hasRating = computed(
  () => props.rating !== null && props.rating !== undefined,
);

const ratingText = computed(() => {
  const r = props.rating;
  if (r === null || r === undefined) return "";
  if (r > 0) return `+${r}`;
  return r.toString();
});

const ratingClass = computed(() => {
  const r = props.rating;
  if (r === null || r === undefined) return "";
  if (r > 0) return "positive";
  if (r < 0) return "negative";
  return "neutral";
});
</script>

<template>
  <span v-if="hasRating" class="post-rating" :class="ratingClass">{{
    ratingText
  }}</span>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Variables"
@import "src/assets/styles/Themes"

// Post rating - colored text like old DM site
.post-rating
  font-weight: bold

  &.positive
    color: $accent-green

  &.negative
    color: $accent-red

  &.neutral
    color: $text-muted
</style>
