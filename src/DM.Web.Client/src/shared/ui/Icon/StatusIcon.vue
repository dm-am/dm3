<template>
  <div class="status-icon" :class="`status-icon--${type}`">
    <span v-if="type === 'success'" class="status-symbol">{{ symbols.checkmark }}</span>
    <span v-else-if="type === 'warning'" class="status-symbol">{{ symbols.warning }}</span>
    <span v-else-if="type === 'error'" class="status-symbol">{{ symbols.close }}</span>
  </div>
</template>

<script setup lang="ts">
import { symbols } from "@/shared/lib/utils/icons";

defineProps<{
  type: "success" | "warning" | "error";
}>();
</script>

<style scoped lang="sass">
@import "@/assets/styles/Variables"

.status-icon
  width: 48px
  height: 48px
  margin: 0 auto $medium
  display: flex
  align-items: center
  justify-content: center

.status-symbol
  font-size: 2rem
  line-height: 1

// Success state with animation
.status-icon--success
  color: $accent-green
  animation: status-entrance 0.4s ease-out

// Warning state
.status-icon--warning
  color: $text-muted

// Error state
.status-icon--error
  color: $accent-red

// Animations
@keyframes status-entrance
  0%
    opacity: 0
    transform: scale(0.9) translateY(8px)
  100%
    opacity: 1
    transform: scale(1) translateY(0)

// Mobile adjustments
@media (max-width: 480px)
  .status-icon
    width: 36px
    height: 36px
</style>
