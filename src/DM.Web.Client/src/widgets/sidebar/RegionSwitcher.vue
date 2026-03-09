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
import MenuBlock from "@/widgets/menu/MenuBlock.vue";
import { useRegion } from "@/shared/lib/composables/useRegion";

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
// Variables are injected globally via vite.config.ts additionalData

.region-button
  display: flex
  align-items: center
  gap: $small
  width: 100%
  padding: $small
  background: transparent
  border: 1px solid $border
  border-radius: $border-radius
  cursor: pointer
  color: $text
  transition: all $animation-time ease

  &:hover:not(:disabled)
    background: $bg-element-hover
    border-color: $link

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
