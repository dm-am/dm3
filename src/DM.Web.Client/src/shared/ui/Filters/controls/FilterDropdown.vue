<script setup lang="ts">
// The panel of a filter menu; Escape inside it asks to close.
import { ref } from "vue";
import { useKeepInWindow } from "./useKeepInWindow";

const emit = defineEmits<{
  close: [];
}>();

// Inside the window whatever edge its button stands at (useKeepInWindow).
const panel = ref<HTMLElement | null>(null);
useKeepInWindow(panel);
</script>

<template>
  <div
    ref="panel"
    class="filter-dropdown-container"
    @keydown.esc="emit('close')"
  >
    <slot />
  </div>
</template>

<style scoped lang="sass">
@use "@/assets/styles/Filters" as *

.filter-dropdown-container
  +dropdown-panel

// The level of a date range holds a calendar under its fields (DateRangePicker),
// about 460px of panel. The 300 of a list would cut it in half and scroll the
// calendar inside a box its own height, so this level grows to what it holds
// and scrolls only when the window itself is lower than that.
.filter-dropdown-container:has(.dropdown-date-range)
  max-height: calc(100vh - #{$big * 2})
</style>
