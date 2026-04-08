<template>
  <svg
    :viewBox="icon.viewBox"
    :fill="icon.fill || 'none'"
    :stroke="icon.stroke"
    :stroke-width="icon.strokeWidth"
    :stroke-linecap="icon.strokeLinecap"
    :stroke-linejoin="icon.strokeLinejoin"
    class="svg-icon"
    v-html="icon.path"
  />
</template>

<script setup lang="ts">
import { computed } from "vue";
import { icons, type IconName } from "@/shared/lib/utils/icons";

const props = defineProps<{
  /** Icon name from icons.ts */
  name: IconName;
}>();

const icon = computed(() => icons[props.name]);
</script>

<style scoped lang="sass">
.svg-icon
  width: 1em
  height: 1em
  flex-shrink: 0
</style>
