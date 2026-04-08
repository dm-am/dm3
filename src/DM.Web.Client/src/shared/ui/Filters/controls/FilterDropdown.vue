<script setup lang="ts">
/**
 * FilterDropdown - Dropdown container with navigation support.
 *
 * Manages dropdown state and hierarchical navigation.
 */
import { FilterDropdownHeader } from "../primitives";

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

function handleBack() {
  emit("back");
}
</script>

<template>
  <div class="filter-dropdown-container">
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
@import "src/assets/styles/Filters"

.filter-dropdown-container
  position: absolute
  top: calc(100% + $tiny)
  left: auto
  right: 0
  min-width: $filter-dropdown-min-width
  z-index: $z-dropdown
  max-height: $filter-dropdown-max-height
  overflow-y: auto
  background-color: $bg-element
  border: 1px solid $border
  border-radius: $border-radius
  box-shadow: 0 4px 12px $shadow-color
</style>
