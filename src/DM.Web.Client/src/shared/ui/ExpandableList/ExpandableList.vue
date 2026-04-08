<script setup lang="ts">
/**
 * ExpandableList - Unified accordion-style expandable list
 *
 * Used for rules sections, FAQ, etc.
 * Visual style unified with DataTable (gap-based borders).
 */

import { useExpandable } from "@/shared/lib/composables/useExpandable";

export interface ExpandableItem {
  id: string;
  title: string;
  /** Content as string array (rendered as bullet list) or plain string */
  content?: string | string[];
}

const props = withDefaults(
  defineProps<{
    /** Array of expandable items */
    items: ExpandableItem[];
    /** Whether to allow multiple items to be expanded (default: false - accordion mode) */
    allowMultiple?: boolean;
  }>(),
  {
    allowMultiple: false,
  },
);

defineSlots<{
  /** Custom content slot for each item */
  content: (props: { item: ExpandableItem; index: number }) => void;
}>();

const { toggle, isExpanded } = useExpandable();

function handleKeydown(event: KeyboardEvent, id: string) {
  if (event.key === "Enter" || event.key === " ") {
    event.preventDefault();
    toggle(id);
  }
}
</script>

<template>
  <div class="expandable-list" role="list">
    <template v-for="(item, index) in items" :key="item.id">
      <div
        class="expandable-row"
        :class="{ expanded: isExpanded(item.id) }"
        role="button"
        tabindex="0"
        :aria-expanded="isExpanded(item.id)"
        :aria-controls="`expandable-details-${item.id}`"
        @click="toggle(item.id)"
        @keydown="handleKeydown($event, item.id)"
      >
        <span class="expand-icon" aria-hidden="true">{{
          isExpanded(item.id) ? "▼" : "▶"
        }}</span>
        <span class="item-title">{{ item.title }}</span>
      </div>
      <div
        v-if="isExpanded(item.id)"
        :id="`expandable-details-${item.id}`"
        class="expandable-details"
      >
        <!-- Custom slot content -->
        <slot name="content" :item="item" :index="index">
          <!-- Default: render content as bullet list or plain text -->
          <template v-if="Array.isArray(item.content)">
            <ul>
              <li v-for="(line, lineIdx) in item.content" :key="lineIdx">
                {{ line }}
              </li>
            </ul>
          </template>
          <template v-else-if="item.content">
            {{ item.content }}
          </template>
        </slot>
      </div>
    </template>
  </div>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Variables"
@import "src/assets/styles/Themes"
@import "src/assets/styles/Tables"

.expandable-list
  +table

.expandable-row
  user-select: text
  +expandable-row

.expand-icon
  user-select: none
  +expand-icon

.item-title
  font-weight: 500

.expandable-details
  user-select: text
  cursor: text
  +expandable-details
  +expandable-details-list

@media (max-width: $mobile-breakpoint)
  .expandable-row
    +expandable-row-mobile

  .expandable-details
    +expandable-details-mobile
</style>
