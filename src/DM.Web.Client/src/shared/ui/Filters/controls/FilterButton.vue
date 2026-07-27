<script setup lang="ts">
/**
 * FilterButton - Filter toggle button (controlled component).
 *
 * Shows "Фильтры" button. Parent manages dropdown state and wrapper.
 */
import { ref } from "vue";
import { SvgIcon } from "@/shared/ui/Icon";

defineOptions({ name: "FilterButton" });

const props = withDefaults(
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
  close: [];
}>();

const buttonRef = ref<HTMLButtonElement | null>(null);

function handleClick(event: MouseEvent) {
  emit("click", event);
}

// Dropdown state/wrapper is owned by the parent (see component doc comment).
// On Escape we ask the parent to close it and return focus to this trigger.
function handleEscape() {
  if (!props.active) return;
  emit("close");
  buttonRef.value?.focus();
}
</script>

<template>
  <button
    ref="buttonRef"
    type="button"
    class="filter-btn"
    :class="{ active }"
    :aria-expanded="active"
    @click="handleClick"
    @keydown.esc="handleEscape"
  >
    <SvgIcon name="filter" class="filter-icon" />
    <span>{{ label }}</span>
  </button>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Inputs"

// Icon-specific additions only — base visual comes from the unified +button mixin.
.filter-btn
  justify-content: flex-start
  gap: $small
  +button

.filter-icon
  width: 20px
  height: 20px
  flex-shrink: 0
</style>
