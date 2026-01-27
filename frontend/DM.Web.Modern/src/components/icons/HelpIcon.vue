<script setup lang="ts">
/**
 * HelpIcon - SVG icons for help links
 *
 * Icons:
 * - question: speech bubble with ? (1-B)
 * - warning: triangle with ! (2-A)
 * - bug: long beetle with 6 legs (3-A)
 * - idea: simple lightbulb (4-B)
 *
 * Style: 24x24 viewBox, stroke 1.5px, currentColor
 */

import type { HelpIconType } from "@/constants/helpLinks";

defineProps<{
  type: HelpIconType;
}>();
</script>

<template>
  <svg
    class="help-icon"
    viewBox="0 0 24 24"
    fill="none"
    stroke="currentColor"
    stroke-width="1.5"
    stroke-linecap="round"
    stroke-linejoin="round"
  >
    <!-- question: speech bubble with ? (1-B) -->
    <template v-if="type === 'question'">
      <path
        d="M21 11.5a8.38 8.38 0 0 1-.9 3.8 8.5 8.5 0 0 1-7.6 4.7 8.38 8.38 0 0 1-3.8-.9L3 21l1.9-5.7a8.38 8.38 0 0 1-.9-3.8 8.5 8.5 0 0 1 4.7-7.6 8.38 8.38 0 0 1 3.8-.9h.5a8.48 8.48 0 0 1 8 8v.5z"
      />
      <path d="M9.5 9a2.5 2.5 0 0 1 5 0c0 1.5-2.5 2-2.5 3" />
      <circle cx="12" cy="15.5" r="0.5" fill="currentColor" stroke="none" />
    </template>

    <!-- warning: triangle with ! (2-A) -->
    <template v-else-if="type === 'warning'">
      <path
        d="M10.29 3.86L1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3L13.71 3.86a2 2 0 0 0-3.42 0z"
      />
      <line x1="12" y1="9" x2="12" y2="13" />
      <circle cx="12" cy="16" r="0.75" fill="currentColor" stroke="none" />
    </template>

    <!-- bug: long beetle with 6 legs (3-A) -->
    <template v-else-if="type === 'bug'">
      <ellipse cx="12" cy="14" rx="4" ry="6" />
      <line x1="12" y1="8" x2="12" y2="20" />
      <circle cx="12" cy="5" r="2" />
      <path d="M10 3.5L8 2" />
      <path d="M14 3.5l2-1.5" />
      <!-- 6 лапок -->
      <path d="M8 10L5 8" />
      <path d="M16 10l3-2" />
      <path d="M8 14L4 14" />
      <path d="M16 14l4 0" />
      <path d="M8 18l-3 2" />
      <path d="M16 18l3 2" />
    </template>

    <!-- idea: simple lightbulb (4-B) -->
    <template v-else-if="type === 'idea'">
      <path
        d="M12 2a6 6 0 0 0-4.24 10.24c.58.58 1.24 1.4 1.24 2.26v1.5h6v-1.5c0-.86.66-1.68 1.24-2.26A6 6 0 0 0 12 2z"
      />
      <line x1="9" y1="18" x2="15" y2="18" />
      <line x1="9" y1="20" x2="15" y2="20" />
      <line x1="10" y1="22" x2="14" y2="22" />
    </template>
  </svg>
</template>

<style scoped lang="sass">
.help-icon
  width: 22px
  height: 22px
  flex-shrink: 0
</style>
