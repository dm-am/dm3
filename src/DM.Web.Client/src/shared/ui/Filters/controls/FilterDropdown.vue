<script setup lang="ts">
/**
 * FilterDropdown - Dropdown container with navigation support.
 *
 * Manages dropdown state and hierarchical navigation.
 */
import { ref } from "vue";
import { FilterDropdownHeader } from "../primitives";
import { useKeepInWindow } from "./useKeepInWindow";

defineOptions({ name: "FilterDropdown" });

defineProps<{
  /** Navigation title (shown in header when navigating) */
  navTitle?: string;
  /** Whether navigation header should be shown */
  showNavHeader?: boolean;
}>();

const emit = defineEmits<{
  back: [];
  close: [];
}>();

// Inside the window whatever edge its button stands at (useKeepInWindow).
const panel = ref<HTMLElement | null>(null);
useKeepInWindow(panel);

function handleBack() {
  emit("back");
}

function handleEscape() {
  emit("close");
}
</script>

<template>
  <div
    ref="panel"
    class="filter-dropdown-container"
    @keydown.esc="handleEscape"
  >
    <!-- Navigation header -->
    <FilterDropdownHeader
      v-if="showNavHeader && navTitle"
      :title="navTitle"
      @back="handleBack"
    />

    <!-- Content -->
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
