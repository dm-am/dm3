<script setup lang="ts">
// Nothing in the window is red but a refusal: the danger is in the words, read rather than recognised.
// The caret starts on "Отмена": Enter pressed without reading takes the way out.
import { computed, ref, watch } from "vue";
import Button from "@/shared/ui/Button/Button.vue";
import { useDialogShell } from "@/shared/lib/composables/useDialogShell";
import {
  useDialogAction,
  type DialogAction,
  type DialogActionResult,
} from "@/shared/lib/composables/useDialogAction";

const props = withDefaults(
  defineProps<{
    show: boolean;
    title: string;
    message?: string;
    confirmLabel?: string;
    /**
     * What the main button does: a request that resolves to the client's
     * `{ data, error }`, or a function that asks no server. The window runs
     * it, shows the wait and the refusal, and closes after success.
     */
    action?: DialogAction;
    /**
     * The refusal line's sentence for a failure the server did not word
     * ("Не удалось удалить файл"). The server's own sentence wins over it.
     */
    failure?: string;
    /** Not carried to <body>: over a vue-final-modal window, whose focus trap would take the caret back. */
    inline?: boolean;
  }>(),
  {
    message: "",
    confirmLabel: "Подтвердить",
    action: undefined,
    failure: undefined,
    inline: false,
  },
);

const emit = defineEmits<{
  (e: "update:show", value: boolean): void;
  /** Pressed with no action to run: the caller does the rest. */
  (e: "confirm"): void;
  /** The action succeeded; the window closes after this. */
  (e: "done", result: DialogActionResult): void;
  /** The window closed with no result: the reader left, or the session ended. */
  (e: "cancel"): void;
}>();

const container = ref<HTMLElement | null>(null);
const cross = ref<HTMLButtonElement | null>(null);

function close() {
  emit("update:show", false);
}

const request = useDialogAction({
  onSuccess: (result) => {
    emit("done", result);
    close();
  },
  onExpired: () => {
    emit("cancel");
    close();
  },
  failure: () => props.failure,
  spare: () => cross.value,
});
const { busy, refusal, waiting } = request;

const shell = useDialogShell({
  show: computed(() => props.show),
  container,
  // Found by class: a ref on the Button component is the instance, not the element.
  initialFocus: () =>
    container.value?.querySelector<HTMLButtonElement>(".dialog-btn-cancel"),
  onDismiss: () => handleCancel(),
});

// A window that opens again starts from a clean slate: the refusal of the
// last time was about the last time.
watch(
  () => props.show,
  (open) => {
    if (open) request.reset();
  },
);

function handleConfirm() {
  if (busy.value) return;
  if (props.action) void request.run(props.action);
  else emit("confirm");
}

function handleCancel() {
  if (busy.value) return;
  emit("cancel");
  close();
}
</script>

<template>
  <Teleport to="body" :disabled="inline">
    <Transition name="dialog">
      <div
        v-if="show"
        class="dialog-backdrop"
        @mousedown="shell.handleBackdropPress"
        @click="shell.handleBackdropClick"
        @keydown="shell.handleKeydown"
      >
        <!-- tabindex="-1": a click on the text would move focus to <body>, above the backdrop's Escape
             listener. alertdialog: the window interrupts to state a consequence and take a decision. -->
        <div
          ref="container"
          class="dialog-container"
          tabindex="-1"
          role="alertdialog"
          aria-modal="true"
          :aria-label="title"
        >
          <button
            ref="cross"
            type="button"
            class="dialog-close"
            aria-label="Закрыть"
            :aria-disabled="busy || undefined"
            @click="handleCancel"
          ></button>

          <h3 class="dialog-title">{{ title }}</h3>
          <p v-if="message" class="dialog-message">{{ message }}</p>
          <p v-if="refusal" class="dialog-refusal" role="alert">
            {{ refusal }}
          </p>

          <div class="dialog-actions">
            <Button
              type="button"
              variant="primary"
              class="dialog-btn-submit"
              :loading="busy"
              :disabled="waiting"
              @click="handleConfirm"
            >
              {{ confirmLabel }}
            </Button>
            <Button
              type="button"
              class="dialog-btn-cancel"
              :disabled="busy"
              @click="handleCancel"
            >
              Отмена
            </Button>
          </div>
        </div>
      </div>
    </Transition>
  </Teleport>
</template>

<style scoped lang="sass">
@use "@/assets/styles/ZIndex" as *
@use "@/assets/styles/DialogSurface" as *

$confirm-step: $dialog-gutter * .5

// No blur: the question is about what the reader was just looking at. margin: auto starts a tall card at the top.
.dialog-backdrop
  position: fixed
  inset: 0
  z-index: $z-dialog
  display: flex
  box-sizing: border-box
  padding: $medium
  overflow-y: auto
  overscroll-behavior: contain
  background-color: $overlay-bg

.dialog-container
  +dialog-card
  display: flex
  flex-direction: column
  // A gap, not margins: a window with no sentence keeps the same distance to the row. Half the card's
  // gutter: the parts of a confirmation stand closer to each other than to its edge.
  gap: $confirm-step
  width: $dialog-width-compact
  margin: auto
  text-align: center

.dialog-close
  +dialog-close

// The reserve on both sides keeps the centring honest against the cross.
.dialog-title
  +dialog-title
  padding: 0 $dialog-gutter

.dialog-message
  margin: 0
  color: $text
  line-height: 1.5

.dialog-refusal
  +dialog-refusal

.dialog-actions
  +dialog-actions
  margin-top: $confirm-step

// Fades only: a card that also scaled drew attention to its own arrival.
.dialog-enter-active,
.dialog-leave-active
  transition: opacity $transition-slow
  @media (prefers-reduced-motion: reduce)
    transition: none

.dialog-enter-from,
.dialog-leave-to
  opacity: 0
</style>
