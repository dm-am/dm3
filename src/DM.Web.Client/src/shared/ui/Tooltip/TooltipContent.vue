<script setup lang="ts">
import { computed, ref } from "vue";

/**
 * Renders text with inline tooltip markup:
 * - [tipimg:URL]text[/tipimg] — hover on text shows image popup
 * - [tip:caption]text[/tip] — hover on text shows text popup
 */
const props = withDefaults(
  defineProps<{
    text?: string;
  }>(),
  {
    text: "",
  },
);

interface TextSegment {
  type: "text" | "tipimg" | "tip";
  content: string;
  imageUrl?: string;
  tipText?: string;
}

// Parse [tipimg:URL]text[/tipimg] and [tip:text]text[/tip] markup
const segments = computed<TextSegment[]>(() => {
  const text = props.text || "";
  if (!text) return [];

  const result: TextSegment[] = [];
  const regex = /\[(tipimg|tip):([^\]]+)\]([\s\S]*?)\[\/\1\]/g;
  let lastIndex = 0;
  let match;

  while ((match = regex.exec(text)) !== null) {
    if (match.index > lastIndex) {
      result.push({
        type: "text",
        content: text.slice(lastIndex, match.index),
      });
    }

    const tagType = match[1] as "tipimg" | "tip";
    const param = match[2];
    const hoverText = match[3];

    if (tagType === "tipimg") {
      result.push({
        type: "tipimg",
        content: hoverText,
        imageUrl: param,
      });
    } else {
      result.push({
        type: "tip",
        content: hoverText,
        tipText: param,
      });
    }

    lastIndex = regex.lastIndex;
  }

  if (lastIndex < text.length) {
    result.push({
      type: "text",
      content: text.slice(lastIndex),
    });
  }

  if (result.length === 0 && text) {
    return [{ type: "text" as const, content: text }];
  }

  return result;
});

// Popup state for nested hover - stores which segment index is active
const activeSegmentIdx = ref<number | null>(null);
let hideTimer: ReturnType<typeof setTimeout> | null = null;

function showPopup(idx: number) {
  if (hideTimer) {
    clearTimeout(hideTimer);
    hideTimer = null;
  }
  activeSegmentIdx.value = idx;
}

function scheduleHidePopup() {
  hideTimer = setTimeout(() => {
    activeSegmentIdx.value = null;
  }, 200);
}

function cancelHidePopup() {
  if (hideTimer) {
    clearTimeout(hideTimer);
    hideTimer = null;
  }
}

// Get active segment data
const activeSegment = computed(() => {
  if (activeSegmentIdx.value === null) return null;
  return segments.value[activeSegmentIdx.value];
});

const activeImageUrl = computed(() => {
  const segment = activeSegment.value;
  if (!segment || segment.type !== "tipimg") return null;
  return segment.imageUrl || null;
});

// Pictures that failed to load, keyed by URL. The failure is state Vue renders
// from, not a node the handler swaps by hand: replacing the <img> markup from
// the event took the element out from under the patcher, so Vue went on
// believing the node was there and the next update of this subtree worked on
// an element no longer in the document. Keyed by URL and not by segment index
// because the indices shift whenever the text is re-parsed.
const failedImageUrls = ref(new Set<string>());

const activeImageFailed = computed(() => {
  const url = activeImageUrl.value;
  return url !== null && failedImageUrls.value.has(url);
});

// Takes the URL the failing element was rendered with: an error can arrive
// after the pointer has moved on, and the active segment is no longer the one
// that failed by then.
function markImageFailed(url: string) {
  failedImageUrls.value = new Set(failedImageUrls.value).add(url);
}
</script>

<template>
  <span class="rich-text">
    <!-- Fallback if no segments parsed -->
    <template v-if="segments.length === 0">{{ text }}</template>

    <!-- Text content with interactive markers -->
    <template v-for="(segment, idx) in segments" :key="idx">
      <!-- Plain text -->
      <template v-if="segment.type === 'text'">{{ segment.content }}</template>

      <!-- Interactive text that shows popup on hover -->
      <span
        v-else
        class="rich-text-trigger"
        @mouseenter="showPopup(idx)"
        @mouseleave="scheduleHidePopup"
        >{{ segment.content
        }}<span
          v-if="activeSegmentIdx === idx && activeSegment"
          class="rich-text-popup"
          @mouseenter="cancelHidePopup"
          @mouseleave="scheduleHidePopup"
          ><template v-if="activeImageUrl"
            ><span v-if="activeImageFailed" class="popup-error"
              >Не удалось загрузить изображение</span
            ><img
              v-else
              :src="activeImageUrl"
              alt=""
              class="popup-image"
              @error="markImageFailed(activeImageUrl)" /></template
          ><template v-else>{{ activeSegment.tipText }}</template></span
        ></span
      >
    </template>
  </span>
</template>

<style lang="sass">
@use "@/assets/styles/ZIndex" as *

// Global (not scoped): TooltipContent renders inside the teleported tooltip.
.rich-text-trigger
  position: relative
  text-decoration: underline dotted
  text-decoration-color: currentColor
  text-underline-offset: 3px
  cursor: help

.rich-text-popup
  position: absolute
  bottom: 100%
  left: 50%
  transform: translateX(-50%)
  margin-bottom: $small
  padding: $small $medium
  background-color: $tooltip-bg
  color: $tooltip-text
  border: 1px solid $tooltip-border
  box-shadow: 0 2px 8px $shadow-color
  border-radius: $small
  white-space: nowrap
  // The tooltip tier of the scale. The literal 10001 stood one above $z-toast,
  // which the scale marks "always on top": a [tip:] opened while an error toast
  // was on screen covered the error the toast was reporting.
  z-index: $z-tooltip

  .popup-image
    display: block
    max-width: 280px
    max-height: 200px
    width: auto
    height: auto
    border-radius: $minor

  // The theme's red. The message used to carry a colour literal of its own,
  // one of the two left in the client, and belonged to no palette.
  .popup-error
    color: $accent-red
</style>
