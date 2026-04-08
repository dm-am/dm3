<script setup lang="ts">
/**
 * FilterButton - Filter toggle button (controlled component).
 *
 * Shows "Фильтры" button. Parent manages dropdown state and wrapper.
 */
import { SvgIcon } from "@/shared/ui/Icon";

defineOptions({ name: "FilterButton" });

withDefaults(
  defineProps<{
    /** Button label */
    label?: string;
    /** Whether button is in active state */
    active?: boolean;
  }>(),
  {
    label: "Фильтры",
    active: false,
  },
);

const emit = defineEmits<{
  click: [event: MouseEvent];
}>();

function handleClick(event: MouseEvent) {
  emit("click", event);
}
</script>

<template>
  <button
    type="button"
    class="filter-btn"
    :class="{ active }"
    @click="handleClick"
  >
    <SvgIcon name="filter" class="filter-icon" />
    <span>{{ label }}</span>
  </button>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Filters"

// Only button styles, wrapper handled by parent
.filter-btn
  display: inline-flex
  align-items: center
  justify-content: flex-start
  gap: $small
  height: $filter-control-height
  box-sizing: border-box
  padding: 0 $medium
  font-size: $secondary-font-size
  font-family: inherit
  white-space: nowrap
  cursor: pointer
  border: 1px solid $border
  border-radius: $border-radius
  background-color: $bg-element
  color: $text

  &:hover,
  &.active
    background-color: $bg-element-accent

.filter-icon
  width: 20px
  height: 20px
  flex-shrink: 0
</style>
