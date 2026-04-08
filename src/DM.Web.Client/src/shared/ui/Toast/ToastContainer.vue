<template>
  <Teleport to="body">
    <div class="toast-container" v-if="toasts.length" aria-live="polite">
      <TransitionGroup name="toast">
        <div
          v-for="toast in toasts"
          :key="toast.id"
          class="toast-item"
          :class="`toast-${toast.type}`"
          role="alert"
          :aria-live="toast.type === 'error' ? 'assertive' : 'polite'"
          @mouseenter="pause(toast.id)"
          @mouseleave="resume(toast.id)"
        >
          <span class="toast-message">{{ toast.message }}</span>
          <button
            class="toast-dismiss"
            @click="dismiss(toast.id)"
            aria-label="Закрыть"
          >
            &times;
          </button>
          <div
            v-if="toast.duration > 0"
            class="toast-progress"
            :style="{ width: `${(toast.remaining / toast.duration) * 100}%` }"
          />
        </div>
      </TransitionGroup>
    </div>
  </Teleport>
</template>

<script setup lang="ts">
import { useToast } from "@/shared/lib/composables/useToast";

const { toasts, dismiss, pause, resume } = useToast();
</script>

<style scoped lang="sass">
@import "src/assets/styles/Variables"
@import "src/assets/styles/Themes"
@import "src/assets/styles/ZIndex"

.toast-container
  position: fixed
  bottom: $medium
  right: $medium
  z-index: $z-toast
  display: flex
  flex-direction: column
  gap: $small
  max-width: 400px

.toast-item
  position: relative
  display: flex
  align-items: center
  justify-content: space-between
  padding: $small $medium
  border-radius: $border-radius
  font-size: $secondary-font-size
  box-shadow: 0 2px 8px $shadow-color
  min-width: 250px
  overflow: hidden

.toast-success
  background-color: var(--toast-success-bg)
  color: var(--toast-success-text)
  border-left: 3px solid var(--toast-success-border)

.toast-error
  background-color: var(--toast-error-bg)
  color: var(--toast-error-text)
  border-left: 3px solid var(--toast-error-border)

.toast-info
  background-color: var(--toast-info-bg)
  color: var(--toast-info-text)
  border-left: 3px solid var(--toast-info-border)

.toast-warning
  background-color: var(--toast-warning-bg)
  color: var(--toast-warning-text)
  border-left: 3px solid var(--toast-warning-border)

.toast-message
  flex: 1
  margin-right: $small

.toast-dismiss
  background: none
  border: none
  color: inherit
  font-size: 1.2rem
  cursor: pointer
  padding: 0 $tiny
  opacity: 0.8
  &:hover
    opacity: 1

.toast-progress
  position: absolute
  bottom: 0
  left: 0
  height: 3px
  background: $overlay-subtle
  transition: width 0.1s linear
  @media (prefers-reduced-motion: reduce)
    transition: none

.toast-enter-active,
.toast-leave-active
  transition: all $animation-time ease
  @media (prefers-reduced-motion: reduce)
    transition: none

.toast-enter-from
  opacity: 0
  transform: translateX(100%)

.toast-leave-to
  opacity: 0
  transform: translateX(100%)
</style>
