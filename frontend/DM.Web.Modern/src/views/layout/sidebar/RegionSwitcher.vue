<template>
  <menu-block v-if="isHydrated && canSwitch" token="RegionSwitcher">
    <template #title>Зеркало сайта</template>
    <button
      type="button"
      class="region-button"
      :class="{ 'is-loading': isTransferring }"
      :disabled="!canSwitch || isTransferring"
      @click="switchRegion"
    >
      <span class="region-icon">{{ currentRegion?.id === 'main' ? '🌐' : '🇷🇺' }}</span>
      <span class="region-text">{{ switchTooltip }}</span>
    </button>
  </menu-block>
</template>

<script setup lang="ts">
import MenuBlock from "@/views/layout/MenuBlock.vue";
import { useRegion } from "@/composables/useRegion";

const {
  currentRegion,
  canSwitch,
  switchTooltip,
  switchRegion,
  isHydrated,
  isTransferring,
} = useRegion();
</script>

<style scoped lang="sass">
@import "src/assets/styles/Variables"
@import "src/assets/styles/Themes"

.region-button
  display: flex
  align-items: center
  gap: $small
  width: 100%
  padding: $small
  background: transparent
  border: 1px solid $border-color
  border-radius: $border-radius
  cursor: pointer
  color: $text-color
  transition: all $animation-time ease

  &:hover:not(:disabled)
    background: $hover-background
    border-color: $link-color

  &:disabled
    cursor: not-allowed
    opacity: 0.5

  &.is-loading
    animation: pulse 1s infinite

.region-icon
  font-size: 1.2em

.region-text
  flex: 1
  text-align: left

@keyframes pulse
  0%, 100%
    opacity: 0.7
  50%
    opacity: 0.3
</style>
