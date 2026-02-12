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
          <button class="toast-dismiss" @click="dismiss(toast.id)" aria-label="Закрыть">&times;</button>
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
import { useToast } from "@/composables/useToast";

const { toasts, dismiss, pause, resume } = useToast();
</script>

<style scoped lang="sass">
.toast-container
  position: fixed
  bottom: 1rem
  right: 1rem
  z-index: 10000
  display: flex
  flex-direction: column
  gap: 0.5rem
  max-width: 400px

.toast-item
  position: relative
  display: flex
  align-items: center
  justify-content: space-between
  padding: 0.75rem 1rem
  border-radius: 4px
  color: #fff
  font-size: 0.9rem
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.2)
  min-width: 250px
  overflow: hidden

.toast-success
  background-color: var(--accent-green, #4caf50)

.toast-error
  background-color: var(--accent-red, #f44336)

.toast-info
  background-color: var(--link, #304060)

.toast-warning
  background-color: var(--accent-red, #b22222)

.toast-message
  flex: 1
  margin-right: 0.5rem

.toast-dismiss
  background: none
  border: none
  color: inherit
  font-size: 1.2rem
  cursor: pointer
  padding: 0 0.25rem
  opacity: 0.8
  &:hover
    opacity: 1

.toast-progress
  position: absolute
  bottom: 0
  left: 0
  height: 3px
  background: rgba(255, 255, 255, 0.4)
  transition: width 0.1s linear

.toast-enter-active,
.toast-leave-active
  transition: all 0.3s ease

.toast-enter-from
  opacity: 0
  transform: translateX(100%)

.toast-leave-to
  opacity: 0
  transform: translateX(100%)
</style>
