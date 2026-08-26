<template>
  <SpeechBubble
    ref="bubbleRef"
    :expandable="expandable"
    :content-key="testimonial.id"
  >
    <!-- Plain text only, NO BBCode. -->
    <span
      v-if="searchQuery"
      v-html="highlightMatch(displayText, searchQuery)"
    /><template v-else>{{ displayText }}</template>

    <!-- The author — the person "speaking" in the bubble — always leads.
         With `about` set (both profile recommendation lists) the line extends
         to "<author> о <recipient>", recipient as a muted link. Spaces around
         "о" are real text nodes ({{ " " }}) so the whole line copies as plain
         text (same pattern as Tabs.vue). -->
    <template #left>
      <user-link
        :user="testimonial.author"
        :search-query="searchQuery"
        :hide-badge="true"
      /><template v-if="about"
        >{{ " " }}<span class="about-connector">о</span>{{ " "
        }}<user-link
          class="about-link"
          :user="about"
          :search-query="searchQuery"
          :hide-badge="true"
      /></template>
    </template>

    <template #right>
      <Tooltip :text="formatDateFull(testimonial.createdUtc)" focusable
        ><secondary-text class="testimonial-date">
          {{ formatDate(testimonial.createdUtc) }}
        </secondary-text></Tooltip
      ><slot name="controls"></slot>
    </template>
  </SpeechBubble>
</template>

<script setup lang="ts">
/**
 * A recommendation in the shared speech bubble: plain text in the bubble,
 * "<author> о <recipient>" and the date in its footer.
 *
 * The bubble itself — geometry, tail, three-line collapse and the chevron —
 * lives in shared/ui/SpeechBubble, because a game review is drawn by the same
 * shape and the two must not be two copies of one stylesheet.
 */
import { ref, computed } from "vue";
import type { WebsiteTestimonial } from "@/shared/api/models/community";
import type { UserRef } from "@/shared/api/models/common";
import { UserLink } from "@/entities/user/@x/testimonial";
import { SpeechBubble } from "@/shared/ui/SpeechBubble";
import { Tooltip } from "@/shared/ui/Tooltip";
import { highlightMatch } from "@/shared/lib/utils/highlight";
import { formatDate, formatDateFull } from "@/shared/lib/utils/datetime";

const props = withDefaults(
  defineProps<{
    testimonial: WebsiteTestimonial;
    /** Enable expand/collapse for long text (gallery mode) */
    expandable?: boolean;
    /** Search query for highlighting */
    searchQuery?: string;
    /**
     * Who the testimonial is ABOUT — set by both profile recommendation
     * lists, received and written. Extends the footer to
     * "<author> о <recipient>": the author keeps the regular footer link
     * treatment (the person "speaking" in the bubble stays primary), the
     * recipient renders as a muted link. The line is composed here and
     * nowhere else: a caller says whether there is a recipient at all,
     * never how the two names are joined.
     * When unset, the footer names only the author — the
     * /about/testimonials gallery and the home page show reviews of the
     * site itself, which have no recipient.
     */
    about?: UserRef;
  }>(),
  {
    expandable: false,
    searchQuery: undefined,
    about: undefined,
  },
);

// Trimmed original text — removes accidental leading/trailing blank lines
// so they never eat into the 3-line truncation budget.
const displayText = computed(() => props.testimonial.text.trim());

// The rotating gallery drives measurement and reads the expanded state to
// avoid swapping a testimonial out mid-read; both belong to the bubble now,
// so the contract is forwarded rather than reimplemented.
const bubbleRef = ref<InstanceType<typeof SpeechBubble> | null>(null);
defineExpose({
  measureContent: () => bubbleRef.value?.measureContent(),
  get isExpanded() {
    return bubbleRef.value?.isExpanded ?? false;
  },
});
</script>

<style scoped lang="sass">
@use "@/assets/styles/Inputs" as *

// Recipient link in the "<author> о <recipient>" footer line (both profile
// recommendation lists): recedes to the muted treatment so the
// author — the person "speaking" in the bubble — stays the visually
// primary link. :deep is required for the <a>: UserLink's root <span>
// receives this component's scope attribute via class fallthrough, but
// the link inside it does not.
.about-link
  :deep(a)
    +muted-link

// The "о" connector shares the muted treatment of the recipient link —
// the whole "о <recipient>" clause recedes as one quiet unit.
.about-connector
  color: $text-muted

// secondary-text renders a block <div>; inside the float's inline copy
// flow both must be inline, or Chrome emits a newline at their block
// boundaries and the footer copies as "author\ndate" again.
.testimonial-date
  display: inline
  font-size: $font-size
  cursor: help
</style>
