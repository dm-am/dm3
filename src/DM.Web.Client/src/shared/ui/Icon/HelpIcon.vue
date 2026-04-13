<script setup lang="ts">
/**
 * HelpIcon - SVG icons for help links
 *
 * Uses SvgIcon component with named icons from icons.ts.
 */

import type { HelpIconType } from "@/shared/config/helpLinks";
import { SvgIcon } from "@/shared/ui/Icon";

defineProps<{
  type: HelpIconType;
}>();
</script>

<template>
  <span class="help-icon" :class="type">
    <SvgIcon :name="type" />
  </span>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Variables"

.help-icon
  display: inline-flex
  :deep(svg)
    width: 28px
    height: 28px
    flex-shrink: 0
  &.bug :deep(svg path)
    stroke: $bg-page
    stroke-width: 1
</style>
