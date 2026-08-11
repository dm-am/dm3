<script setup lang="ts">
/**
 * ConfirmDialog — a modal confirmation prompt (title + message + confirm/cancel).
 *
 * Reuses the InputDialog modal shell (backdrop / container / header / footer,
 * Escape-to-cancel, backdrop-click-to-cancel) but carries a read-only message
 * instead of input fields. Used for destructive/irreversible confirms
 * (premoderation, delete game, reset recruitment, delete NPC, room delete).
 *
 * The `danger` flag paints the confirm button in the accent-red action color.
 * A minimal focus trap keeps Tab within the dialog while it is open and
 * restores focus to the previously-focused element on close.
 */
import { computed, ref } from "vue";
import { symbols } from "@/shared/lib/utils/icons";
import { useDialogShell } from "@/shared/lib/composables/useDialogShell";

const props = withDefaults(
  defineProps<{
    show: boolean;
    title: string;
    message?: string;
    confirmLabel?: string;
    cancelLabel?: string;
    /** Render the confirm action as destructive (accent-red). */
    danger?: boolean;
    /** Disable the confirm button and show a spinner-ish disabled state. */
    loading?: boolean;
  }>(),
  {
    message: "",
    confirmLabel: "Подтвердить",
    cancelLabel: "Отмена",
    danger: false,
    loading: false,
  },
);

const emit = defineEmits<{
  (e: "update:show", value: boolean): void;
  (e: "confirm"): void;
  (e: "cancel"): void;
}>();

const container = ref<HTMLElement | null>(null);
const confirmBtn = ref<HTMLButtonElement | null>(null);

const shell = useDialogShell({
  show: computed(() => props.show),
  container,
  initialFocus: () => confirmBtn.value,
  onDismiss: () => handleCancel(),
});

function close() {
  emit("update:show", false);
}

function handleConfirm() {
  if (props.loading) return;
  emit("confirm");
}

function handleCancel() {
  emit("cancel");
  close();
}

function handleKeydown(e: KeyboardEvent) {
  if (shell.handleKeydown(e)) return;

  if (e.key === "Enter") {
    // Enter confirms only when focus is NOT on one of the dialog buttons —
    // otherwise a focused "Отмена"/close button would trigger the (possibly
    // destructive) confirm instead of its own native Enter activation.
    const active = document.activeElement;
    if (
      active instanceof HTMLButtonElement &&
      container.value?.contains(active)
    ) {
      return;
    }
    e.preventDefault();
    handleConfirm();
  }
}
</script>

<template>
  <Teleport to="body">
    <Transition name="dialog">
      <div
        v-if="show"
        class="dialog-backdrop"
        @click="shell.handleBackdropClick"
        @keydown="handleKeydown"
      >
        <!-- tabindex="-1" is not decoration. The Escape listener sits on the
             backdrop, and a click on the dialog's own heading or sentence moves
             the document focus to <body>; the keydown then fires on body, which
             is the backdrop's ANCESTOR, and never reaches the listener — so
             Escape stopped closing the dialog the reader was looking at. A
             container that can hold focus itself takes that click instead, and
             the event has a path back down. -->
        <div
          ref="container"
          class="dialog-container"
          tabindex="-1"
          role="dialog"
          aria-modal="true"
          :aria-label="title"
        >
          <div class="dialog-header">
            <h3 class="dialog-title">{{ title }}</h3>
            <button
              type="button"
              class="dialog-close"
              aria-label="Закрыть"
              @click="handleCancel"
            >
              {{ symbols.close }}
            </button>
          </div>

          <div v-if="message" class="dialog-content">
            <p class="dialog-message">{{ message }}</p>
          </div>

          <div class="dialog-footer">
            <button
              type="button"
              class="dialog-btn dialog-btn-cancel"
              @click="handleCancel"
            >
              {{ cancelLabel }}
            </button>
            <button
              ref="confirmBtn"
              type="button"
              class="dialog-btn dialog-btn-submit"
              :class="{ danger }"
              :disabled="loading"
              @click="handleConfirm"
            >
              {{ confirmLabel }}
            </button>
          </div>
        </div>
      </div>
    </Transition>
  </Teleport>
</template>

<style scoped lang="sass">
@import "@/assets/styles/Inputs"
@import "@/assets/styles/ZIndex"
@import "@/assets/styles/Animations"

.dialog-backdrop
  position: fixed
  inset: 0
  z-index: $z-dialog
  display: flex
  align-items: center
  justify-content: center
  background-color: $overlay-bg
  backdrop-filter: blur(2px)

.dialog-container
  width: 100%
  max-width: 420px
  margin: $medium
  border-radius: $border-radius
  box-shadow: 0 8px 32px var(--shadow-color)
  background-color: $bg-element
  border: 1px solid $border

.dialog-header
  display: flex
  justify-content: space-between
  align-items: center
  padding: $medium
  border-bottom: 1px solid
  border-color: $border

.dialog-title
  margin: 0
  font-size: $font-size
  font-weight: 600

.dialog-close
  padding: 4px 8px
  border: none
  background: none
  font-size: 20px
  cursor: pointer
  color: $text-muted

  &:hover
    color: $text

.dialog-content
  padding: $medium

.dialog-message
  margin: 0
  color: $text
  line-height: 1.5

.dialog-footer
  display: flex
  justify-content: flex-start
  gap: $small
  padding: $medium
  border-top: 1px solid
  border-color: $border

.dialog-btn
  font-size: $secondary-font-size
  +button

  &.danger
    +button-danger

  &:disabled
    opacity: 0.6
    cursor: default

// Transition animations
.dialog-enter-active,
.dialog-leave-active
  transition: opacity 0.2s ease
  @media (prefers-reduced-motion: reduce)
    transition: none

.dialog-enter-from,
.dialog-leave-to
  opacity: 0

  .dialog-container
    transform: scale(0.95)

.dialog-enter-active .dialog-container,
.dialog-leave-active .dialog-container
  transition: transform 0.2s ease
  @media (prefers-reduced-motion: reduce)
    transition: none
</style>
