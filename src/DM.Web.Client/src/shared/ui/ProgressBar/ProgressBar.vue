<script setup lang="ts">
import { computed } from "vue";

const props = defineProps<{ goal: number; current: number }>();
const progress = computed(() => (props.current / props.goal) * 100);
</script>

<template>
  <div class="progress">
    <div class="progress-scale" :style="{ width: `${progress}%` }" />
    <div class="progress-text"><slot /></div>
  </div>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Themes"

.progress
  position: relative
  overflow: hidden
  margin: $small 0
  padding: $minor
  border-radius: $border-radius
  background-color: $progress-bg-overlay

.progress-scale
  position: absolute
  top: 0
  left: 0
  bottom: 0
  background-color: $progress-fill-overlay
  transition: width 0.4s ease
  @media (prefers-reduced-motion: reduce)
    transition: none

.progress-text
  position: relative
  color: $text
</style>
