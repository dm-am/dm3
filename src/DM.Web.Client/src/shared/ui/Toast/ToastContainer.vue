<script setup lang="ts">
import { computed } from "vue";
import { useToast, type ToastAction } from "@/shared/lib/composables/useToast";

const { toasts, dismiss, pause, resume } = useToast();

/** The press is marked in the message: it belongs to a place in the sentence. */
const MARKED = /^([^[]*)\[([^\]]+)\]([\s\S]*)$/;

const items = computed(() =>
  toasts.value.map((toast) => {
    const found = toast.action ? MARKED.exec(toast.message) : null;
    return {
      toast,
      link: found
        ? { before: found[1], label: found[2], after: found[3] }
        : null,
    };
  }),
);

function act(id: string, run?: ToastAction) {
  run?.();
  dismiss(id);
}
</script>

<template>
  <Teleport to="body">
    <!-- No aria-live here: the container mounts with the first toast; each toast announces itself. -->
    <div class="toast-container" v-if="toasts.length">
      <TransitionGroup name="toast">
        <div
          v-for="{ toast, link } in items"
          :key="toast.id"
          class="toast-item"
          :class="`toast-${toast.type}`"
          :role="toast.type === 'error' ? 'alert' : 'status'"
          @mouseenter="pause(toast.id)"
          @mouseleave="resume(toast.id)"
        >
          <span v-if="link" class="toast-message"
            >{{ link.before
            }}<button class="toast-action" @click="act(toast.id, toast.action)">
              {{ link.label }}</button
            >{{ link.after }}</span
          >
          <span v-else class="toast-message">{{ toast.message }}</span>
          <button
            type="button"
            class="toast-dismiss"
            @click="dismiss(toast.id)"
            aria-label="Закрыть"
          ></button>
          <!-- The browser runs the bar down and holds it with the clock; a new run draws a new bar. -->
          <div
            v-if="toast.duration > 0"
            :key="toast.run"
            class="toast-progress"
            :class="{ 'toast-progress--held': toast.paused }"
            :style="{ animationDuration: `${toast.duration}ms` }"
          />
        </div>
      </TransitionGroup>
    </div>
  </Teleport>
</template>

<style scoped lang="sass">
@use "@/assets/styles/ZIndex" as *
@use "@/assets/styles/Links" as *
@use "@/assets/styles/DialogSurface" as *

// Underlined in the message's ink, as a link inside a paragraph is drawn.
.toast-action
  +inline-link-button
  color: inherit
  text-decoration: underline

// Steps aside of the ScrollNav rail, which shares the corner two tiers below.
.toast-container
  position: fixed
  bottom: $medium
  right: $medium + $scroll-nav-width + $small
  z-index: $z-toast
  display: flex
  flex-direction: column
  gap: $small
  // Each message as wide as its words; the cap is over the longest line (toastWidth.spec.ts), so nothing wraps.
  align-items: flex-end
  max-width: 620px

// The theme's surface and ink, framed in the accent of the kind: a shadow alone is not an edge.
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
  box-shadow: $shadow-small
  // As wide as the words, and never wider than the column.
  max-width: 100%
  overflow: hidden

// The whole frame in the kind's tone: a one-edge accent rule is the mark of a quote and of [mod].
.toast-success
  background-color: $bg-highlight-green
  border-color: $border-accent-green

.toast-error
  background-color: $bg-highlight-red
  border-color: $border-accent-red

.toast-info
  background-color: $bg-highlight-blue
  border-color: $border-accent-blue

.toast-warning
  background-color: $bg-highlight-yellow
  border-color: $border-accent-yellow

.toast-message
  flex: 0 1 auto
  margin-right: $small

// The windows' cross, standing in the row of the message rather than in a corner.
.toast-dismiss
  +dialog-close

  &
    position: relative
    top: auto
    right: auto
    flex: none

.toast-progress
  position: absolute
  bottom: 0
  left: 0
  width: 100%
  height: 3px
  background: $overlay-subtle
  animation: toast-time linear forwards
  // Run down in 0.01ms (Reset.sass) it would be gone at once: the bar stays whole instead.
  @media (prefers-reduced-motion: reduce)
    animation: none

.toast-progress--held
  animation-play-state: paused

@keyframes toast-time
  to
    width: 0

.toast-enter-active,
.toast-leave-active
  transition: opacity $transition-slow, transform $transition-slow
  @media (prefers-reduced-motion: reduce)
    transition: none

.toast-enter-from,
.toast-leave-to
  opacity: 0
  transform: translateX(100%)
</style>
