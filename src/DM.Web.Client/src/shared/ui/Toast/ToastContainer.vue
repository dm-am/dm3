<template>
  <Teleport to="body">
    <!-- No aria-live on the container: it mounts together with the first
         toast (v-if), so a container live region would not pre-exist in the
         DOM and the first announcement would be lost. Per-toast role="alert"
         / role="status" announces on insertion instead — exactly one live
         region per toast, no double announcements. -->
    <div class="toast-container" v-if="toasts.length">
      <TransitionGroup name="toast">
        <div
          v-for="toast in toasts"
          :key="toast.id"
          class="toast-item"
          :class="`toast-${toast.type}`"
          :role="toast.type === 'error' ? 'alert' : 'status'"
          @mouseenter="pause(toast.id)"
          @mouseleave="resume(toast.id)"
        >
          <span class="toast-message">{{ toast.message }}</span>
          <button
            class="toast-dismiss"
            @click="dismiss(toast.id)"
            aria-label="Закрыть"
          >
            {{ symbols.close }}
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
import { symbols } from "@/shared/lib/utils/icons";

const { toasts, dismiss, pause, resume } = useToast();
</script>

<style scoped lang="sass">
@use "@/assets/styles/ZIndex" as *

// Pinned to the bottom-right corner, and so is the ScrollNav rail — which
// sits on $z-sticky, two tiers below the toast. A toast used to land on top
// of the scroll buttons and the settings one, hiding the controls a reader
// reaches for while the message is still on screen. Stepping aside by the
// width of the rail plus a gap keeps both usable; the rail measures its own
// $medium from the edge.
.toast-container
  position: fixed
  bottom: $medium
  right: $medium + $scroll-nav-width + $small
  z-index: $z-toast
  display: flex
  flex-direction: column
  gap: $small
  max-width: 400px

// A panel like every other panel on the site: the surface of the theme, the
// ink of the theme, and a rule down the left edge in the accent that says
// which kind of message this is. It used to be twelve hand-picked Material
// colours - a saturated fill with white letters that appeared nowhere else
// on the page and did not belong to either palette. The frame is what keeps
// the panel off whatever it floats over; the shadow alone is not an edge.
.toast-item
  position: relative
  display: flex
  align-items: center
  justify-content: space-between
  padding: $small $medium
  border: 1px solid $border
  border-radius: $border-radius
  font-size: $secondary-font-size
  color: $text
  box-shadow: 0 2px 8px $shadow-color
  min-width: 250px
  overflow: hidden

// The left rule comes after the frame above and overrides that one edge.
.toast-success
  background-color: $bg-highlight-green
  border-left: 3px solid $accent-green

.toast-error
  background-color: $bg-highlight-red
  border-left: 3px solid $accent-red

.toast-info
  background-color: $bg-highlight-blue
  border-left: 3px solid $link

.toast-warning
  background-color: $bg-highlight-yellow
  border-left: 3px solid $accent-yellow

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
  transition: opacity 0.3s ease, transform 0.3s ease
  @media (prefers-reduced-motion: reduce)
    transition: none

.toast-enter-from
  opacity: 0
  transform: translateX(100%)

.toast-leave-to
  opacity: 0
  transform: translateX(100%)
</style>
