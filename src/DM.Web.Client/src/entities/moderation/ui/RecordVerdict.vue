<script setup lang="ts">
/**
 * One decision under a moderation record, drawn as a review of a post is
 * (PostReviewItem.vue): the words as a point of the list in the colour of the
 * text, and under them one caption line, "<Что> от <кого>, <когда>", where the
 * kind stands in the place and the colour of the review's sign. The kind is a
 * noun phrase that reads whole before "от" (warningLabels.ts):
 * "Предупреждение (2 балла)", "Изменение баллов с 3 на 1".
 *
 * The owner (28.09): "Почему изменения баллов оформлены как контент, а не как
 * предупреждение? ... И предупреждения, как и изменения баллов, должны быть
 * оформлены, как оценки постов". So a warning and a change of its points are
 * two items of one kind: what was decided, who decided, when. The tone of the
 * kind is the review's sign, and the caller sets it: red for a kind that
 * names points, grey for one that names none (a verbal warning, a change to
 * nothing, WarningCard.vue). A ban is no item
 * here: the list of bans writes each one as DM2's bot did, in the colour of
 * the text (BanLogCard.vue).
 *
 * The caller adds its controls at the end of the caption, after a bar, the way
 * a review adds "Редактировать" and "Удалить".
 *
 * The caption wraps between its parts at every width, as the captions of
 * topics and comments do: on a phone it is the caption of the full version,
 * with nothing put away for want of room (owner, 12 October 2026: no "Еще",
 * no "…", no name cut). What was decided and by whom is one part, the date
 * with its time another, so a narrow line breaks after the comma and takes the
 * date to the next line whole, and the controls after it.
 *
 * Both halves of the caption are optional: an item with neither kind nor date
 * is the words alone, with no caption line at all.
 *
 * The words are a text of the site like the text of a comment (CommentItem):
 * flush on both sides at every width, as DM2 set every text block, and broken
 * at syllables while the reader's switch "Перенос слов" is on.
 */
import { storeToRefs } from "pinia";
import { CaptionLine, CaptionPart } from "@/shared/ui/CaptionLine";
import { formatDateFull } from "@/shared/lib/utils/datetime";
import { useUiStore } from "@/shared/stores/ui";

withDefaults(
  defineProps<{
    /** What kind of decision it was: "Предупреждение (2 балла)". */
    kind?: string | null;
    /** Red for a kind that names points, neutral for one that names none. */
    tone?: "negative" | "neutral";
    /** Who decided, by name; absent for an account that is gone. */
    author?: string | null;
    /** When; absent for an item that only says what came of it. */
    date?: string | null;
  }>(),
  { kind: null, tone: "neutral", author: null, date: null },
);

const { hyphenateWords } = storeToRefs(useUiStore());
</script>

<template>
  <li class="record-verdict">
    <div
      v-if="$slots.default"
      class="record-verdict__text"
      :class="{ hyphenated: hyphenateWords }"
    >
      <slot />
    </div>
    <!-- Wraps between its parts, as the caption of the record above it
         (RecordBlock). -->
    <div v-if="kind || date" class="record-verdict__caption">
      <CaptionLine>
        <CaptionPart v-if="kind" joint="," class="record-verdict__signature"
          ><span class="record-verdict__kind" :class="`is-${tone}`">{{
            kind
          }}</span
          ><template v-if="author"
            >{{ " от\u00a0"
            }}<router-link
              :to="{ name: 'profile', params: { username: author } }"
              >{{ author }}</router-link
            ></template
          ></CaptionPart
        >
        <span v-if="date" class="record-verdict__date">{{
          formatDateFull(date)
        }}</span>
        <slot name="controls" />
      </CaptionLine>
    </div>
  </li>
</template>

<style scoped lang="sass">
@use "@/assets/styles/BbcodeContent" as *

// PostReviewItem.vue .review-item: the item's margin and indent, its line
// height, and the clear step between two of them (owner, 27.09).
.record-verdict
  margin: $minor 0
  padding-left: $small
  line-height: 1.4

  & + &
    margin-top: $medium

// PostReviewItem.vue .review-text: the words in the colour of the text, a
// little heavier than the body, a small step over the caption.
.record-verdict__text
  color: $text
  font-weight: 500
  margin-bottom: $small
  overflow-wrap: anywhere
  // The text of a comment (CommentItem.vue, .comment-text): flush on both
  // sides, broken at syllables while the switch is on. The line stays the
  // review's, the item's above, and not the comment's.
  +body-text
  line-height: inherit

  // An item that only says what came of it has no caption to step down to.
  &:last-child
    margin-bottom: 0

// The caption's size on the block as well, so the line it stands on is the
// caption's height and not the words'.
.record-verdict__caption
  font-size: $tertiary-font-size

// PostReviewItem.vue .review-sign: the colours of the review's sign, red and
// grey (green, the sign of praise, stands on no moderation decision).
.record-verdict__kind
  &.is-negative
    color: $accent-red

  &.is-neutral
    color: $text-muted
</style>
