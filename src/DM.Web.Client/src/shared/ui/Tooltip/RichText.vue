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
      >{{ segment.content }}<span
          v-if="activeSegmentIdx === idx && activeSegment"
          class="rich-text-popup"
          @mouseenter="cancelHidePopup"
          @mouseleave="scheduleHidePopup"
        ><template v-if="activeSegment.type === 'tipimg' && activeSegment.imageUrl"><img
              :src="activeSegment.imageUrl"
              alt=""
              class="popup-image"
              @error="($event.target as HTMLImageElement).outerHTML = '<span style=\'color: #ff6b6b;\'>Ошибка загрузки GIF</span>'"
            /></template><template v-else-if="activeSegment.type === 'tip' && activeSegment.tipText">{{
            activeSegment.tipText
          }}</template><template v-else>[Debug: type={{ activeSegment.type }}]</template></span></span>
    </template>
  </span>
</template>

<style>
/* Global styles (not scoped - RichText is inside teleported tooltip) */
.rich-text-trigger {
  position: relative;
  text-decoration: underline dotted;
  text-decoration-color: currentColor;
  text-underline-offset: 3px;
  cursor: help;
}

.rich-text-popup {
  position: absolute;
  bottom: 100%;
  left: 50%;
  transform: translateX(-50%);
  margin-bottom: 8px;
  padding: 8px 16px;
  background-color: var(--tooltip-bg);
  color: var(--tooltip-text);
  border: 1px solid var(--tooltip-border);
  border-radius: 8px;
  box-shadow: 0 2px 8px var(--shadow-color);
  white-space: nowrap;
  z-index: 10001;
}

.rich-text-popup .popup-image {
  display: block;
  max-width: 280px;
  max-height: 200px;
  width: auto;
  height: auto;
  border-radius: 4px;
}
</style>
