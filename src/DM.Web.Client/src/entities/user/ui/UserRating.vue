<script setup lang="ts">
import { computed } from "vue";
import type { User, UserRef } from "../model/types";
import { Tooltip } from "@/shared/ui/Tooltip";
import { VALUE_UNAVAILABLE } from "@/shared/lib/constants/copy";

const props = defineProps<{
  user: User | UserRef;
}>();

const rating = computed(() => {
  const r = (props.user as User).rating;
  if (!r) return null;
  return {
    reviewSum: r.postReviewScoreSum ?? 0,
    postCount: r.totalPosts ?? 0,
  };
});

const reviewSumDisplay = computed(() => {
  if (!rating.value) return "";
  const v = rating.value.reviewSum;
  return v > 0 ? `+${v}` : String(v);
});

const reviewSumClass = computed(() => {
  if (!rating.value) return "muted";
  if (rating.value.reviewSum > 0) return "positive";
  if (rating.value.reviewSum < 0) return "negative";
  return "muted";
});

const target = computed(() => ({
  name: "received-reviews" as const,
  params: { username: props.user.username },
}));
</script>

<template>
  <span v-if="rating" class="user-rating">
    <Tooltip text="Сумма полученных оценок"
      ><router-link :to="target" class="review-sum" :class="reviewSumClass">{{
        reviewSumDisplay
      }}</router-link></Tooltip
    ><span class="sep">/</span
    ><Tooltip text="Количество постов" focusable
      ><span class="post-count">{{ rating.postCount }}</span></Tooltip
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
.user-rating
  white-space: nowrap

.review-sum
  font-weight: bold
  color: $link
  text-decoration: none

  &.positive
    color: $accent-green

  &.negative
    color: $accent-red

  &.muted
    color: $text-muted

  &:hover
    text-decoration: underline

.sep
  color: $text-muted
  margin: 0 0.15em

.post-count
  color: $text
  cursor: help

.user-rating-na
  font-weight: bold
  color: $text-muted
  text-decoration: none

  &:hover
    text-decoration: underline
</style>
