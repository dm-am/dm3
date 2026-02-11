<template>
  <Teleport to="body">
    <div class="toast-container" v-if="toasts.length">
      <TransitionGroup name="toast">
        <div
          v-for="toast in toasts"
          :key="toast.id"
          class="toast-item"
          :class="`toast-${toast.type}`"
        >
          <span class="toast-message">{{ toast.message }}</span>
          <button class="toast-dismiss" @click="dismiss(toast.id)">&times;</button>
        </div>
      </TransitionGroup>
    </div>
  </Teleport>
</template>

<script setup lang="ts">
import { useToast } from "@/composables/useToast";

const { toasts, dismiss } = useToast();
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
  display: flex
  align-items: center
  justify-content: space-between
  padding: 0.75rem 1rem
  border-radius: 4px
  color: #fff
  font-size: 0.9rem
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.2)
  min-width: 250px

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
