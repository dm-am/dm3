<script setup lang="ts">
import { computed } from "vue";
import type { Rating, User, UserRef } from "../model/types";
import { Tooltip } from "@/shared/ui/Tooltip";
import { VALUE_UNAVAILABLE } from "@/shared/lib/constants/copy";

const props = defineProps<{
  user: UserRef & Partial<Pick<User, "rating">>;
  /** For a user served without one, as the author in a game roster. */
  rating?: Rating | null;
}>();

const shown = computed(() => {
  const r = props.rating ?? props.user.rating;
  if (!r) return null;
  return {
    reviewSum: r.postReviewScoreSum ?? 0,
    postCount: r.totalPosts ?? 0,
  };
});

const reviewSumDisplay = computed(() => {
  if (!shown.value) return "";
  const v = shown.value.reviewSum;
  return v > 0 ? `+${v}` : String(v);
});

const reviewSumClass = computed(() => {
  if (!shown.value) return "muted";
  if (shown.value.reviewSum > 0) return "positive";
  if (shown.value.reviewSum < 0) return "negative";
  return "muted";
});

const target = computed(() => ({
  name: "received-reviews" as const,
  params: { username: props.user.username },
}));
</script>

<template>
  <span v-if="shown" class="user-rating">
    <Tooltip text="Сумма полученных оценок"
      ><router-link :to="target" class="review-sum" :class="reviewSumClass">{{
        reviewSumDisplay
      }}</router-link></Tooltip
    ><span class="sep">/</span
    ><Tooltip text="Количество постов" focusable
      ><span class="post-count">{{ shown.postCount }}</span></Tooltip
    >
  </span>
  <router-link
    v-else
    :to="target"
    class="user-rating user-rating-na"
    aria-label="Полученные оценки: пока нет"
    >{{ VALUE_UNAVAILABLE }}</router-link
  >
</template>

<style scoped lang="sass">
@use "@/assets/styles/Links" as *

.user-rating
  white-space: nowrap

// Rests in the colour of the sum and hovers blue, as every link of DM2.
.review-sum
  font-weight: bold
  +link-tone($link)

  &.positive
    color: $accent-green

  &.negative
    color: $accent-red

  &.muted
    color: $text-muted

.sep
  color: $text-muted
  margin: 0 0.15em

.post-count
  color: $text
  cursor: help

.user-rating-na
  font-weight: bold
  +muted-link
</style>
