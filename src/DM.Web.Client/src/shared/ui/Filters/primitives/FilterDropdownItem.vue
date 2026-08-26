<script setup lang="ts">
/**
 * FilterDropdownItem - Single item in filter dropdown.
 *
 * Displays a clickable item with optional avatar, label, hint, and navigation arrow.
 */
import { SvgIcon } from "@/shared/ui/Icon";
import { highlightMatch } from "@/shared/lib/utils/highlight";
import type { DropdownItemProps } from "../types";

defineOptions({ name: "FilterDropdownItem" });

withDefaults(defineProps<DropdownItemProps>(), {
  hint: undefined,
  avatarUrl: undefined,
  highlighted: false,
  indent: false,
  hasSubOptions: false,
});

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
      <span
        v-if="searchQuery"
        class="item-label"
        v-html="highlightMatch(label, searchQuery)"
      />
      <span v-else class="item-label">{{ label }}</span>
      <span v-if="hint" class="item-hint">{{ hint }}</span>
    </span>
    <SvgIcon v-if="hasSubOptions" name="chevronRight" class="item-arrow" />
  </button>
</template>

<style scoped lang="sass">
@use "@/assets/styles/Filters" as *

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

  &.indented
    padding-left: $large

  &:hover,
  &.highlighted
    background-color: $bg-element-accent

.item-avatar
  width: $filter-avatar-size
  height: $filter-avatar-size
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
