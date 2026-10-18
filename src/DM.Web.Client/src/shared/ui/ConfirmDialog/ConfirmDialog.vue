<script setup lang="ts">
/**
 * ConfirmDialog — the window that asks before an action the reader cannot take
 * back: a question for a heading, one sentence naming the consequence, and the
 * two ways out standing side by side.
 *
 * It is drawn as a guest card, because a confirmation is the same kind of
 * object as the login dialog: a short card standing over a dimmed page, with
 * the page's own fill, no frame, no shadow and a cross in the corner. The card,
 * the cross, the heading and the row of buttons are the window surface of
 * _DialogSurface.sass, which the window with a form (Layout/Dialog.vue) draws
 * too, so the two windows of the site cannot drift apart.
 *
 * Nothing in the window is red but a refusal. The danger is carried by the
 * words — the heading asks with the verb of the button, the sentence says what
 * is lost — and not by a fill, a border or a caption, so the reader has to read
 * the question rather than recognise a colour. Owner's call, 21 September 2026.
 *
 * The buttons are the site's own: the main action is the primary of
 * shared/ui/Button, the way out is a plain one, and the row is two equal
 * halves at the window's control height. Order follows Form.vue, the action
 * first and "Отмена" after it. The caret starts on "Отмена", the step that
 * loses nothing: a reader who presses Enter without reading has taken the way
 * out, not the action.
 *
 * Handed an `action`, the window runs it (useDialogAction): it stays open until
 * the answer and is shut while it waits, a refusal stands in a line over the
 * row, and it closes on success alone. Without one it only says `confirm`,
 * and the caller closes it: the guard of unsaved input sends no request.
 *
 * A minimal focus trap keeps Tab within the dialog while it is open and
 * restores focus to the previously-focused element on close (useDialogShell).
 */
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
    cancelLabel?: string;
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
    /**
     * The caller runs a request of its own after `confirm`: the window is shut
     * the way it is shut while its own action runs.
     */
    loading?: boolean;
    /**
     * Drawn where it is written and not carried to <body>: the question over a
     * window of the vue-final-modal tier, whose focus trap keeps the caret
     * inside that window and would take it back from a card outside it.
     */
    inline?: boolean;
  }>(),
  {
    message: "",
    confirmLabel: "Подтвердить",
    cancelLabel: "Отмена",
    action: undefined,
    failure: undefined,
    loading: false,
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
const { refusal, waiting } = request;

/** Every way out is shut, and the main button is busy. */
const busy = computed(() => request.busy.value || props.loading);

const shell = useDialogShell({
  show: computed(() => props.show),
  container,
  // "Отмена" is found inside the container rather than held in a template ref,
  // because the row is drawn by the site's Button component and a ref on a
  // component is the instance, not the element.
  initialFocus: () =>
    container.value?.querySelector<HTMLButtonElement>(".dialog-btn-cancel"),
  onDismiss: () => handleCancel(),
  holdPage: true,
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
        <!-- tabindex="-1" is not decoration. The Escape listener sits on the
             backdrop, and a click on the dialog's own heading or sentence moves
             the document focus to <body>; the keydown then fires on body, which
             is the backdrop's ANCESTOR, and never reaches the listener — so
             Escape stopped closing the dialog the reader was looking at. A
             container that can hold focus itself takes that click instead, and
             the event has a path back down.

             role="alertdialog" and not "dialog": this window interrupts the
             page to state a consequence and take a decision, which is the role
             a screen reader announces together with the text inside it. -->
        <div
          ref="container"
          class="dialog-container"
          tabindex="-1"
          role="alertdialog"
          aria-modal="true"
          :aria-label="title"
        >
          <!-- Empty on purpose: the two bars of the cross are drawn by the
               stylesheet, and a glyph inside would stand behind them. -->
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
              {{ cancelLabel }}
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

// The dimmed page under the window, in the token the guest dialogs dim with
// (.vfm__overlay.dialog-overlay, ThemeVariables.css) and with no blur, for the
// same reason they have none: the question is about something the reader was
// just looking at, and a blur takes it away while he decides.
//
// It is also the frame that scrolls a card taller than the screen, the way the
// window with a form scrolls: the card centres itself with `margin: auto`,
// which starts one that does not fit at the top instead of cutting it at both
// ends, and the padding is the edge the card keeps from the screen.
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

// The card of the window surface at the compact measure, the only one this
// window has: what it holds is a question and a single sentence, so there is
// nothing for a second width to do.
.dialog-container
  +dialog-card
  display: flex
  flex-direction: column
  // The rhythm of the card: this gap between the question and the sentence
  // under it, and this gap plus the row's own margin before the buttons.
  // Written as a gap rather than as margins on the blocks, so a window with no
  // sentence in it keeps the same distance from the question to the row
  // instead of growing a strip of nothing where the sentence would have been.
  gap: 12px
  width: $dialog-width-compact
  margin: auto
  text-align: center

.dialog-close
  +dialog-close

// The question, in the heading style of the surface and centred, as the
// sentence under it is. The reserve is taken on both sides, so the centring
// stays honest against the cross and a long question wraps before it runs
// under it.
.dialog-title
  +dialog-title
  padding: 0 $dialog-close-reserve

// One sentence, naming what the action costs.
.dialog-message
  margin: 0
  color: $text
  line-height: 1.5

// The refusal, one more line of the card's rhythm and centred with the rest.
.dialog-refusal
  +dialog-refusal

.dialog-actions
  +dialog-actions
  margin-top: 12px

// The window fades in and out and does nothing else. A card that also scaled
// drew attention to its own arrival, which is the one thing a question about
// losing something does not need.
.dialog-enter-active,
.dialog-leave-active
  transition: opacity 0.2s ease
  @media (prefers-reduced-motion: reduce)
    transition: none

.dialog-enter-from,
.dialog-leave-to
  opacity: 0
</style>
