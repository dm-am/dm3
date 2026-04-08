<script setup lang="ts">
/**
 * FilterDropdownItem - Single item in filter dropdown.
 *
 * Displays a clickable item with optional avatar, label, hint, and navigation arrow.
 */
defineOptions({ name: "FilterDropdownItem" });

withDefaults(
  defineProps<{
    /** Display label */
    label: string;
    /** Optional hint/description shown below label */
    hint?: string;
    /** Optional avatar URL */
    avatarUrl?: string;
    /** Whether this item is highlighted (keyboard navigation) */
    highlighted?: boolean;
    /** Whether to indent this item (for hierarchy) */
    indent?: boolean;
    /** Whether this item has sub-options (shows arrow) */
    hasSubOptions?: boolean;
  }>(),
  {
    hint: undefined,
    avatarUrl: undefined,
    highlighted: false,
    indent: false,
    hasSubOptions: false,
  },
);

const emit = defineEmits<{
  "item-select": [];
  mouseenter: [];
}>();

function handleClick() {
  emit("item-select");
}

function handleMouseEnter() {
  emit("mouseenter");
}
</script>

<template>
  <button
    type="button"
    class="dropdown-item"
    :class="{ highlighted, indented: indent }"
    @click.stop="handleClick"
    @mouseenter="handleMouseEnter"
  >
    <img v-if="avatarUrl" :src="avatarUrl" alt="" class="item-avatar" />
    <span class="item-content">
      <span class="item-label">{{ label }}</span>
      <span v-if="hint" class="item-hint">{{ hint }}</span>
    </span>
    <svg
      v-if="hasSubOptions"
      class="item-arrow"
      width="16"
      height="16"
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      stroke-width="2"
    >
      <path d="M9 18l6-6-6-6" />
    </svg>
  </button>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Filters"

.dropdown-item
  display: flex
  align-items: center
  gap: $small
  width: 100%
  padding: $small $medium
  font-size: $secondary-font-size
  font-family: inherit
  text-align: left
  cursor: pointer
  border: none
  background: none
  color: $text
  transition: background-color 0.1s

  &.indented
    padding-left: $large

  &:hover,
  &.highlighted
    background-color: $bg-element-accent

.item-avatar
  width: $filter-avatar-size
  height: $filter-avatar-size
  border-radius: 50%
  flex-shrink: 0

.item-content
  display: flex
  flex-direction: column
  gap: 1px

.item-label
  color: $text

.item-hint
  font-size: $tertiary-font-size
  color: $text-muted

.item-arrow
  margin-left: auto
  color: $text-muted
  flex-shrink: 0
</style>
