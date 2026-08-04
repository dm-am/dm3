<script setup lang="ts">
import { computed } from "vue";
import type { PostReview } from "../model/types";
import { ReviewSign } from "@/shared/api/models/game/reviews";
import { UserLink } from "@/entities/user/@x/game";
import { ContentText } from "@/shared/ui/Content";
import { Tooltip } from "@/shared/ui/Tooltip";
import { SvgIcon } from "@/shared/ui/Icon";
import { formatDateFull } from "@/shared/lib/utils/datetime";
import { getLikesTooltip } from "@/shared/lib/utils/chat";
import { useToast } from "@/shared/lib/composables/useToast";
import type { User } from "@/shared/api/models/common";

const props = withDefaults(
  defineProps<{
    review: PostReview;
    /** Highlights this review's author (bold + accent-green left border) */
    highlight?: boolean;
    /** 1-based position — renders a copyable anchor number (doc 4.2.2.14). */
    number?: number;
    /**
     * Address the number copies: the room page the reviewed post lives on,
     * anchored at the post and naming this review, which is what opens the
     * reviews block on arrival. A review has no page of its own, so the widget
     * that owns the post owns the address (GamePost.vue) — built here it would
     * be the address of whatever surface the card is embedded in, which is how
     * a link to a review on the home page used to point at the home page.
     */
    permalink?: string;
  }>(),
  { highlight: false },
);

const { success: toastSuccess, error: toastError } = useToast();

// One sentence for both the name of the control and the tooltip that describes
// it, the way the comment permalink does it (CommentItem.vue). It says review,
// because the address it copies opens the reviews of the post it lands on and
// marks this one.
const anchorHint = "Скопировать ссылку на отзыв";

async function copyAnchorLink() {
  if (!props.permalink) return;
  try {
    await navigator.clipboard.writeText(props.permalink);
    toastSuccess("Ссылка скопирована");
  } catch {
    toastError("Не удалось скопировать ссылку");
  }
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

const signText = computed(() => getSignText(props.review.sign));

// Likes on a review are read-only here: post reviews have no like/unlike
// endpoint, so this is the static badge CommentItem shows a viewer who cannot
// like (Tooltip with the likers' names, heart, count), not the button.
const likes = computed(() => props.review.likes ?? []);
const likesCount = computed(() => likes.value.length);
// getLikesTooltip only reads usernames; its declared User[] parameter is wider
// than what it consumes, hence the cast from the structural subset.
const likersTooltip = computed(() => getLikesTooltip(likes.value as User[]));
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
      }}<template v-if="number && permalink"
        >{{ ", "
        }}<Tooltip :text="anchorHint"
          ><button
            type="button"
            class="review-anchor"
            :aria-label="anchorHint"
            v-text="`#${number}`"
            @click="copyAnchorLink"
          ></button></Tooltip
      ></template>
      <span v-if="likesCount" class="review-likes"
        >{{ " "
        }}<Tooltip
          :text="likersTooltip"
          focusable
          class="like-static"
          :aria-label="`Нравится: ${likesCount}`"
          ><SvgIcon name="heartEmpty" class="like-icon" /><span
            class="likes-count"
            >{{ likesCount }}</span
          ></Tooltip
        ></span
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

// Read-only like badge, the same idiom CommentItem uses for a viewer who
// cannot like: heart plus count, names in the tooltip. Sized to the meta line
// it trails, not to the comment footer, so it does not outweigh the text.
.like-static
  display: inline-flex
  align-items: center
  gap: 2px
  color: $text-muted
  font-size: $tertiary-font-size

.likes-count
  font-weight: bold

:deep(.author-highlight .user-link)
  font-weight: bold
</style>
