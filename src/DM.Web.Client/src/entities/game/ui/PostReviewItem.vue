<script setup lang="ts">
import { computed } from "vue";
import type { PostReview } from "../model/types";
import { ReviewSign } from "@/shared/api/models/game/reviews";
import { UserLink } from "@/entities/user";
import { ContentText } from "@/shared/ui";
import dayjs from "dayjs";

const props = defineProps<{
  review: PostReview;
}>();

// API returns string values: "Positive", "Negative", "Neutral"
function getSignClass(sign?: ReviewSign | string): string {
  const signStr = String(sign);
  if (signStr === "Positive" || sign === ReviewSign.Positive) return "positive";
  if (signStr === "Negative" || sign === ReviewSign.Negative) return "negative";
  return "neutral";
}

function getSignText(sign?: ReviewSign | string): string {
  const signStr = String(sign);
  if (signStr === "Positive" || sign === ReviewSign.Positive) return "+1";
  if (signStr === "Negative" || sign === ReviewSign.Negative) return "-1";
  return "+0";
}

const formattedDate = computed(() => {
  if (!props.review.createdUtc) return "";
  return dayjs(props.review.createdUtc).format("DD.MM.YYYY [в] HH:mm");
});
</script>

<template>
  <li class="review-item">
    <ContentText v-if="review.text" :html="review.text" class="review-text" />
    <div class="review-meta">
      <span class="review-sign" :class="getSignClass(review.sign)">{{ getSignText(review.sign) }}</span>
      <span class="review-from">от</span>
      <UserLink :user="review.author!" :hide-badge="true" /><!--
      --><span class="review-date">, {{ formattedDate }}</span>
    </div>
  </li>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Variables"
@import "src/assets/styles/Themes"

// Review item - bullet list style like old DM site
.review-item
  margin: $minor 0
  line-height: 1.4

// Review text - first line, primary content
.review-text
  color: $text
  font-weight: 500
  margin-bottom: $small

// Meta line: "+1 от Username, дата"
.review-meta
  font-size: $tertiary-font-size
  color: $text-muted

.review-sign
  margin-right: 4px

  &.positive
    color: $accent-green

  &.negative
    color: $accent-red

  &.neutral
    color: $text-muted

.review-from
  margin-right: 4px

.review-date
  // inherits $text-muted from parent
</style>
