<script setup lang="ts">
/**
 * FilterDropdownHeader - Navigation header for dropdown with back button.
 *
 * Shows a title and back button for hierarchical dropdown navigation.
 */
import { SvgIcon } from "@/shared/ui/Icon";

defineOptions({ name: "FilterDropdownHeader" });

defineProps<{
  /** Title to display */
  title: string;
}>();

const emit = defineEmits<{
  back: [];
}>();
</script>

<template>
  <div class="dropdown-nav-header">
    <button type="button" class="nav-back-btn" @click="emit('back')">
      <SvgIcon name="arrowLeft" />
    </button>
    <span class="nav-title">{{ title }}</span>
    <span class="nav-spacer" />
  </div>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Filters"

.dropdown-nav-header
  display: flex
  align-items: center
  justify-content: space-between
  padding: $small $medium
  background-color: $bg-element-accent
  border-bottom: 1px solid $border

.nav-back-btn
  display: flex
  align-items: center
  justify-content: center
  width: 20px
  height: 20px
  padding: 0
  cursor: pointer
  border: none
  background: none
  color: $text-muted

  &:hover
    color: $text

  :deep(svg)
    width: 14px
    height: 14px

.nav-title
  font-size: $secondary-font-size
  font-weight: 500
  color: $text

.nav-spacer
  width: 20px
  flex-shrink: 0
</style>
