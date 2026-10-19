<script setup lang="ts">
// Drawn as a review of a post, the kind in the place and the colour of the review's sign.
import { storeToRefs } from "pinia";
import { CaptionLine, CaptionPart } from "@/shared/ui/CaptionLine";
import { formatDateFull } from "@/shared/lib/utils/datetime";
import { useUiStore } from "@/shared/stores/ui";

withDefaults(
  defineProps<{
    /** "Предупреждение (2 балла)". */
    kind?: string | null;
    /** Red for a kind that names points. */
    tone?: "negative" | "neutral";
    /** Absent for an account that is gone. */
    author?: string | null;
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
@use "@/assets/styles/ReviewParts" as *

.record-verdict
  +review-item

.record-verdict__text
  +review-text
  overflow-wrap: anywhere
  // The text of a comment, on the line height of the review.
  +body-text
  line-height: inherit

  // An item without a caption.
  &:last-child
    margin-bottom: 0

// On the block too, so the line box is the caption's height.
.record-verdict__caption
  font-size: $tertiary-font-size

// The colours of the review's sign without green, which stands on no moderation decision.
.record-verdict__kind
  &.is-negative
    color: $accent-red

  &.is-neutral
    color: $text-muted
</style>
