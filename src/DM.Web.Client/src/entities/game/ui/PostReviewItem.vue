<script setup lang="ts">
import { computed } from "vue";
import type { PostReview } from "../model/types";
import type { ReviewSign } from "@/shared/api/models/game/reviews";
import type { RouteLocationRaw } from "vue-router";
import { UserLink } from "@/entities/user/@x/game";
import { ContentText } from "@/shared/ui/Content";
import { Tooltip } from "@/shared/ui/Tooltip";
import { formatDateFull } from "@/shared/lib/utils/datetime";

const props = withDefaults(
  defineProps<{
    review: PostReview;
    /** Highlights this review's author (bold + accent-green left border) */
    highlight?: boolean;
    /** 1-based position — renders the anchor number (doc 4.2.2.14). */
    number?: number;
    /**
     * Where the number leads: the room page the reviewed post lives on,
     * anchored at the post and naming this review, which is what opens the
     * reviews block on arrival. A review has no page of its own, so the widget
     * that owns the post owns the address (GamePost.vue): built here it would
     * be the address of whatever surface the card is embedded in, which is how
     * a link to a review on the home page used to point at the home page.
     */
    to?: RouteLocationRaw;
  }>(),
  { highlight: false },
);

// One sentence for both the name of the control and the tooltip that
// describes it. The number goes where it points, the way "Перейти к посту"
// does in the footer of the post itself: a copy button was the odd one out
// among the addresses of this page, and it made the reader paste a link to
// find out where it led. The address stays in the href, so the browser's own
// "copy link" is still there for whoever wants it.
const anchorHint = "Перейти к оценке поста";

// API returns string values: "Positive", "Negative", "Neutral"
function getSignClass(sign?: ReviewSign | string): string {
  const signStr = String(sign);
  if (signStr === "Positive") return "positive";
  if (signStr === "Negative") return "negative";
  return "neutral";
}

function getSignText(sign?: ReviewSign | string): string {
  const signStr = String(sign);
  if (signStr === "Positive") return "+1";
  if (signStr === "Negative") return "-1";
  return "+0";
}

const formattedDate = computed(() => formatDateFull(props.review.createdUtc));

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
      }}</span
      >{{ " от "
      }}<UserLink
        :user="review.author!"
        :hide-badge="true"
        :class="{ 'author-highlight': highlight }"
      />{{ `, ${formattedDate}`
      }}<template v-if="number && to"
        >{{ ", "
        }}<Tooltip :text="anchorHint"
          ><router-link
            class="review-anchor"
            :to="to"
            :aria-label="anchorHint"
            >{{ `#${number}` }}</router-link
          ></Tooltip
        ></template
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

// Meta line: "+1 от Username, дата, #N"
.review-meta
  font-size: $tertiary-font-size
  color: $text-muted

// No margin: the space before the number is the ", " text node above.
.review-anchor
  font-size: $tertiary-font-size
  color: $text-muted
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
