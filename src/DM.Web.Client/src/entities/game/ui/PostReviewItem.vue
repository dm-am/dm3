<script setup lang="ts">
import { computed } from "vue";
import type { PostReview } from "../model/types";
import { ReviewSign } from "@/shared/api/models/game/reviews";
import { UserLink } from "@/entities/user/@x/game";
import { ContentText } from "@/shared/ui";
import { formatDateFull } from "@/shared/lib/utils/datetime";

const props = withDefaults(
  defineProps<{
    review: PostReview;
    /** Highlights this review's author (bold + accent-green left border) */
    highlight?: boolean;
    /** 1-based position — renders a copyable anchor number (doc 4.2.2.14). */
    number?: number;
  }>(),
  { highlight: false },
);

const reviewAnchor = computed(() => `#review-${props.review.id}`);

function copyAnchorLink() {
  navigator.clipboard.writeText(
    window.location.origin + window.location.pathname + reviewAnchor.value,
  );
}

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

const formattedDate = computed(() => formatDateFull(props.review.createdUtc));

// Full meta line built in script — "+1 от Username, DD.MM.YYYY в HH:mm" —
// avoids whitespace-condense gluing the sign/от/date fragments together.
const signText = computed(() => getSignText(props.review.sign));
</script>

<template>
  <li
    :id="`review-${review.id}`"
    class="review-item"
    :class="{ highlighted: highlight }"
  >
    <ContentText v-if="review.text" :html="review.text" class="review-text" />
    <div class="review-meta">
      <span class="review-sign" :class="getSignClass(review.sign)">{{
        signText
      }}</span>
      {{ " от "
      }}<UserLink
        :user="review.author!"
        :hide-badge="true"
        :class="{ 'author-highlight': highlight }"
      />{{ `, ${formattedDate}`
      }}<template v-if="number">
        <button
          type="button"
          class="review-anchor"
          :aria-label="`Скопировать ссылку на отзыв ${number}`"
          title="Скопировать ссылку на отзыв"
          @click="copyAnchorLink"
        >
          #{{ number }}
        </button></template
      >
    </div>
  </li>
</template>

<style scoped lang="sass">
// Review item - bullet list style like old DM site
.review-item
  margin: $minor 0
  padding-left: $small
  line-height: 1.4
  border-left: 2px solid transparent

  &.highlighted
    border-left-color: $accent-green

// Review text - first line, primary content
.review-text
  color: $text
  font-weight: 500
  margin-bottom: $small

// Meta line: "+1 от Username, дата"
.review-meta
  font-size: $tertiary-font-size
  color: $text-muted

.review-anchor
  margin-left: $tiny
  padding: 0
  border: none
  background: none
  font: inherit
  font-size: $tertiary-font-size
  color: $text-muted
  cursor: pointer
  &:hover
    color: $link

.review-sign
  &.positive
    color: $accent-green

  &.negative
    color: $accent-red

  &.neutral
    color: $text-muted

:deep(.author-highlight .user-link)
  font-weight: bold
</style>
