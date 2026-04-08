<template>
  <div class="status-icon" :class="`status-icon--${type}`">
    <!-- Success checkmark -->
    <svg v-if="type === 'success'" viewBox="0 0 24 24" fill="none">
      <polyline
        points="4 12 10 18 20 6"
        stroke="currentColor"
        stroke-width="2.5"
        stroke-linecap="round"
        stroke-linejoin="round"
      />
    </svg>

    <!-- Warning (circle with exclamation) -->
    <svg
      v-else-if="type === 'warning'"
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      stroke-width="2"
    >
      <circle cx="12" cy="12" r="10" />
      <line x1="12" y1="8" x2="12" y2="12" stroke-linecap="round" />
      <line x1="12" y1="16" x2="12.01" y2="16" stroke-linecap="round" />
    </svg>

    <!-- Error (X) -->
    <svg
      v-else-if="type === 'error'"
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      stroke-width="2"
    >
      <line x1="18" y1="6" x2="6" y2="18" stroke-linecap="round" />
      <line x1="6" y1="6" x2="18" y2="18" stroke-linecap="round" />
    </svg>
  </div>
</template>

<script setup lang="ts">
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

  svg
    width: 100%
    height: 100%

// Success state with animation
.status-icon--success
  color: $accent-green
  animation: status-entrance 0.4s ease-out

  svg
    filter: drop-shadow(0 0 8px var(--accent-green-muted))

  svg polyline
    stroke-dasharray: 30
    stroke-dashoffset: 30
    animation: checkmark-draw 0.5s ease-out 0.2s forwards

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

@keyframes checkmark-draw
  0%
    stroke-dashoffset: 30
  100%
    stroke-dashoffset: 0

// Mobile adjustments
@media (max-width: 480px)
  .status-icon
    width: 36px
    height: 36px
</style>
