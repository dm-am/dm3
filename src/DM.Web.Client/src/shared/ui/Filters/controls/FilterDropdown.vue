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
  +_dropdown-panel
</style>
