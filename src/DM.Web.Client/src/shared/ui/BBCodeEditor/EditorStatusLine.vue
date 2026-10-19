<script setup lang="ts">
/**
 * The status line under the editor: how many words and characters the text
 * has, against the limit where there is one, and what became of the draft.
 */
import { computed } from "vue";
import { pluralize } from "@/shared/lib/utils/pluralize";
import type { DraftState } from "./lib/editorDraft";

const props = defineProps<{
  /** The text counted: what the visual mode shows, or the BBCode source. */
  text: string;
  /** 0 = no limit */
  maxLength: number;
  draft: DraftState;
}>();

const charCount = computed(() => props.text.length);

const wordCount = computed(() => {
  const text = props.text;
  if (!text.trim()) return 0;
  return text.trim().split(/\s+/).length;
});

const isOverLimit = computed(
  () => props.maxLength > 0 && charCount.value > props.maxLength,
);

// Built here, not glued in the template: an item of one text node selects as one.
const wordCountLabel = computed(
  () =>
    `${wordCount.value} ${pluralize(wordCount.value, "слово", "слова", "слов")}`,
);

const charCountLabel = computed(() => {
  // The noun agrees with the number right before it: the limit, when there is one.
  const counted = props.maxLength > 0 ? props.maxLength : charCount.value;
  const limit = props.maxLength > 0 ? ` / ${props.maxLength}` : "";
  return `${charCount.value}${limit} ${pluralize(counted, "символ", "символа", "символов")}`;
});

// No relative time: a computed does not tick with the clock.
const draftStatusText = computed(() => {
  if (props.draft === "saving") return "Сохранение";
  if (props.draft === "saved") return "Сохранено";
  return "";
});
</script>

<template>
  <div class="status-bar" role="status" aria-live="polite">
    <span class="status-item">{{ wordCountLabel }}</span
    ><span class="status-separator">{{ " | " }}</span
    ><span class="status-item" :class="{ 'over-limit': isOverLimit }">{{
      charCountLabel
    }}</span
    ><span v-if="draft === 'available'" class="status-separator">{{
      " | "
    }}</span
    ><span v-if="draft === 'available'" class="status-item draft-available"
      >Есть черновик</span
    ><span v-if="draftStatusText" class="status-separator">{{ " | " }}</span
    ><span
      v-if="draftStatusText"
      class="status-item draft-status"
      :class="{ saving: draft === 'saving' }"
      >{{ draftStatusText }}</span
    >
  </div>
</template>

<style scoped lang="sass">
// Inline flow, not flex: a flex item is blockified, and the strip copied as three lines.
.status-bar
  display: block
  padding: $tiny 0
  font-size: 11px
  color: $text-muted

.status-separator
  padding: 0 $minor
  opacity: $muted-opacity

.status-item
  &.over-limit
    color: $accent-red
    font-weight: 700

// The -muted accent variants do not clear AA as a text colour.
.draft-available
  color: $accent-red

.draft-status
  color: $accent-green

  &.saving
    color: $text-muted
</style>
