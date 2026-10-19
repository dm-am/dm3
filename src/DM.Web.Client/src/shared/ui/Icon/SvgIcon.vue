<script setup lang="ts">
import { computed } from "vue";
import {
  icons,
  type IconDefinition,
  type IconName,
} from "@/shared/lib/utils/icons";

const props = defineProps<{
  /** Icon name from icons.ts */
  name: IconName;
}>();

// An unknown name draws a question mark: a throw in render takes the whole subtree down.
// Typed as the interface: `satisfies` keeps each entry's literal shape, without `stroke` where none is drawn.
const icon = computed<IconDefinition>(
  () => icons[props.name] ?? icons.question,
);
</script>

<template>
  <svg
    :viewBox="icon.viewBox"
    :fill="icon.fill || 'none'"
    :stroke="icon.stroke"
    :stroke-width="icon.strokeWidth"
    :stroke-linecap="icon.strokeLinecap"
    :stroke-linejoin="icon.strokeLinejoin"
    class="svg-icon"
    aria-hidden="true"
    focusable="false"
    v-html="icon.path"
  />
</template>

<style scoped lang="sass">
.svg-icon
  width: 1em
  height: 1em
  flex-shrink: 0
</style>
