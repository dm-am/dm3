<template>
  <SpeechBubble
    ref="bubbleRef"
    :expandable="expandable"
    :content-key="testimonial.id"
  >
    <!-- Plain text, no BBCode. -->
    <span
      v-if="searchQuery"
      v-html="highlightMatch(displayText, searchQuery)"
    /><template v-else>{{ displayText }}</template>

    <!-- The spaces around "о" are text nodes, so the line copies as plain text. -->
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
      >
    </template>

    <template v-if="$slots.actions" #actions>
      <slot name="actions" />
    </template>
  </SpeechBubble>
</template>

<script setup lang="ts">
import { ref, computed } from "vue";
import type { WebsiteTestimonial } from "@/shared/api/models/community";
import type { UserRef } from "@/shared/api/models/common";
import { UserLink } from "@/entities/user/@x/testimonial";
import { SpeechBubble } from "@/shared/ui/SpeechBubble";
import { Tooltip } from "@/shared/ui/Tooltip";
import { highlightMatch } from "@/shared/lib/utils/highlight";
import { formatDate, formatDateFull } from "@/shared/lib/utils/datetime";

const props = defineProps<{
  testimonial: WebsiteTestimonial;
  expandable?: boolean;
  searchQuery?: string;
  /** The recipient of a recommendation; a review of the site has none. */
  about?: UserRef;
}>();

// Blank lines at the ends must not eat into the three-line budget.
const displayText = computed(() => props.testimonial.text.trim());

// The rotating gallery measures and reads the expanded state, both the bubble's.
const bubbleRef = ref<InstanceType<typeof SpeechBubble> | null>(null);
defineExpose({
  measureContent: () => bubbleRef.value?.measureContent(),
  get isExpanded() {
    return bubbleRef.value?.isExpanded ?? false;
  },
});
</script>

<style scoped lang="sass">
@use "@/assets/styles/Links" as *

// :deep, since only UserLink's root span gets this component's scope attribute.
.about-link
  :deep(a)
    +muted-link

.about-connector
  color: $text-muted

// Inline, or the footer copies as "author", a newline and the date.
.testimonial-date
  display: inline
  font-size: $font-size
  cursor: help
</style>
