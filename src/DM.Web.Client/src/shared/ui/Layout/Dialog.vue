<script setup lang="ts">
// The window a form opens in: the vue-final-modal tier underneath, the card
// of _DialogSurface.sass on top of it, and the cross in its corner.
//
// The card is the guest card (owner's call, 21 September 2026): the page's
// own fill, 24px of gutter, no frame, no shadow, no blur under it. What the
// window holds is told that it stands in one (IN_DIALOG), so a form inside it
// takes the window's fields and buttons without each of the forms saying so.
//
// A window handed an `action` owns the request the form inside it sends
// (useDialogAction, DIALOG_FORM): it stays open until the answer, is shut
// while it waits, prints a refusal in itself and closes on success alone. The
// owner approved the four of them on 22 September 2026, together with the
// question that guards typed text and the caret's first place. The rules are
// written in UI_STANDARDS.md, "Dialog Standards".
//
// The library closes nothing of a site window by itself. With Escape and the
// click beside the card left to it, a window could not ask before dropping
// typed text or refuse to close while its request is in flight: the library
// reports neither gesture in a way that can be declined. So both are caught
// here and go the one way the cross goes. The guest card (`auto`) keeps the
// library's own closing, as it was approved.
import { computed, provide, ref, watch } from "vue";
import { VueFinalModal } from "vue-final-modal";
import ConfirmDialog from "@/shared/ui/ConfirmDialog/ConfirmDialog.vue";
import {
  useDialogAction,
  type DialogAction,
  type DialogActionResult,
} from "@/shared/lib/composables/useDialogAction";
import { fieldToFocus } from "@/shared/lib/composables/useDialogShell";
import { usePageScrollLock } from "@/shared/lib/composables/usePageScrollLock";
import { useOpenWindow } from "@/shared/lib/composables/useOpenWindows";
import { DIALOG_FORM, IN_DIALOG } from "./dialogContext";

const props = withDefaults(
  defineProps<{
    /**
     * Whether the window is open. A window opened with useModal gets it from
     * the library's container; one written into a template takes v-model.
     */
    modelValue?: boolean;
    /**
     * The window holds the BBCode editor. At 920px the editor's toolbar and its
     * [bbcode]/wysiwyg switch stand in one row; at 580 they took three.
     */
    editor?: boolean;
    /**
     * The guest dialog (AuthDialog), whose content is a card of its own painted
     * by AuthSurface.sass. The wrapper then draws no cross of its own, since the
     * guest card has one, and tells nothing inside it that it is in a window:
     * the guest surface has its own fields.
     */
    auto?: boolean;
    /**
     * What the main button of the form inside does: a request that resolves to
     * the client's `{ data, error }`, or a function that asks no server. The
     * window runs it, shows the wait and the refusal, and closes after success.
     */
    action?: DialogAction;
    /**
     * The refusal line's sentence for a failure the server did not word
     * ("Не удалось изменить баллы"). The server's own sentence wins over it.
     */
    failure?: string;
    /**
     * Multi-line text has been typed in. A click beside the card then does
     * nothing, and Escape, the cross and "Отмена" ask before it is lost.
     */
    dirty?: boolean;
    /** What the question says is lost. */
    discardText?: string;
  }>(),
  { discardText: "Набранный текст пропадет" },
);

const emit = defineEmits<{
  (e: "update:modelValue", open: boolean): void;
  (e: "beforeClose"): void;
  /** The action succeeded; the window closes after this. */
  (e: "done", result: DialogActionResult): void;
  /** The window closed with no result: the reader left, or the session ended. */
  (e: "close"): void;
}>();

// Read once: a window does not stop being the guest's while it is open.
provide(IN_DIALOG, !props.auto);

const card = ref<HTMLElement | null>(null);
const cross = ref<HTMLButtonElement | null>(null);

/** The question "Закрыть без сохранения?" stands over the window. */
const asking = ref(false);

// The question goes with the window. Closed by its own "Закрыть", it is gone
// before it could say so itself: the window it answers for closes first, and
// takes the question's own report of closing with it.
const shut = () => {
  asking.value = false;
  emit("update:modelValue", false);
};

/** Leaves without a result, and asks nothing. */
const leave = () => {
  emit("close");
  shut();
};

const request = useDialogAction({
  onSuccess: (result) => {
    emit("done", result);
    shut();
  },
  onExpired: leave,
  failure: () => props.failure,
  spare: () => cross.value,
});
const { busy } = request;

/**
 * The cross, Escape and "Отмена". Nothing while the request is in flight; the
 * question while typed text would be lost; the way out otherwise.
 */
const requestClose = () => {
  if (busy.value) return;
  if (props.dirty) {
    asking.value = true;
    return;
  }
  leave();
};

/**
 * A click beside the card. It never asks: the gesture is too easy to make by
 * accident to be worth a second window, so over typed text it does nothing.
 */
const closeBeside = () => {
  if (busy.value || props.dirty) return;
  leave();
};

// The session ended (useOpenWindows): the window goes, typed text or not,
// since nothing typed here can be sent any more. A window waiting for its own
// request is left to that request, which meets the same refusal and closes it
// through `onExpired`; closed here as well, it would say "close" twice. The
// guest card stays: it is how a signed-out reader signs in again.
useOpenWindow(
  computed(() => !props.auto && !!props.modelValue),
  () => {
    if (!busy.value) leave();
  },
);

provide(DIALOG_FORM, {
  routed: computed(() => !props.auto && !!props.action),
  submit: () => {
    if (props.action) void request.run(props.action);
  },
  cancel: requestClose,
  busy,
  waiting: request.waiting,
  refusal: request.refusal,
  claim: request.claim,
  fieldError: request.fieldError,
});

const page = usePageScrollLock();

const contentClass = computed(() =>
  props.auto ? "dialog dialog--auto" : "dialog-frame",
);

const cardClass = computed(() =>
  props.editor ? "dialog dialog--editor" : "dialog",
);

/**
 * The caret's first place: what the opener focused itself, if it did; else
 * the first empty field; else the cross. Read by the library's focus trap when
 * it starts, which is after the window is on screen.
 *
 * Focused here and without a scroll, and handed back already focused, which
 * the trap then leaves alone. Focused by the trap, the first empty field of a
 * window taller than the screen scrolled the frame down to itself, and the
 * window opened with its heading out of sight; the trap also selects the text
 * of a field it focuses, and a filled one opened selected.
 */
const initialFocus = (): HTMLElement | undefined => {
  const root = card.value;
  if (!root) return undefined;
  const active = document.activeElement;
  if (active instanceof HTMLElement && root.contains(active)) return active;
  const first = fieldToFocus(root) ?? cross.value ?? undefined;
  first?.focus({ preventScroll: true });
  return first;
};

// Escape is the window's and not the trap's: left to its default, the trap
// switches itself off on Escape and hands the caret to the page, even where
// the window stays open because it is busy or asking.
const focusTrap = computed(() =>
  props.auto
    ? undefined
    : { allowOutsideClick: true, escapeDeactivates: false, initialFocus },
);

// The frame the library draws around the card is not a control, and the
// question over the window cycles Tab through what is left live: without this
// the frame is a stop in the cycle where nothing is visibly focused.
watch(card, (el) => el?.parentElement?.setAttribute("tabindex", "-1"));

// An Escape that a control inside keeps to itself never gets here: the
// question over the window and the calendar of a date field stop it where they
// take it. The editor's input window is a window of its own, drawn from
// <body>, and a key pressed in it does not pass through this one. `defaultPrevented` is no such claim and is not read:
// the editor (ProseMirror) prevents the default of every Escape pressed in it,
// and a window that took that for an answer stayed open over its editor, empty
// or with a typed reason, and asked nothing.
function onKeydown(e: KeyboardEvent) {
  if (props.auto || e.key !== "Escape") return;
  e.preventDefault();
  requestClose();
}

/** A press on the frame around the card, not on its scrollbar. */
const isBeside = (e: MouseEvent): boolean => {
  const target = e.target as HTMLElement | null;
  if (!target?.classList?.contains("dialog-frame")) return false;
  return !(target.clientWidth > 0 && e.offsetX >= target.clientWidth);
};

// The press and the release both beside the card, as the library counts a
// click outside: a selection dragged out of the card is not one.
let pressedBeside = false;

function onPress(e: MouseEvent) {
  pressedBeside = !props.auto && isBeside(e);
}

function onRelease(e: MouseEvent) {
  if (pressedBeside && isBeside(e)) closeBeside();
  pressedBeside = false;
}

/**
 * A link in the card does not lead away while the request is in flight: the
 * screen that opened the window would unmount with it, and the answer would
 * arrive nowhere. The warning window carries one, to the violator's profile.
 */
function holdLinks(e: MouseEvent) {
  if (!busy.value) return;
  if (!(e.target as Element | null)?.closest?.("a[href]")) return;
  e.preventDefault();
  e.stopPropagation();
}
</script>

<template>
  <vue-final-modal
    :model-value="modelValue"
    :content-class="contentClass"
    overlay-class="dialog-overlay"
    overlay-transition="vfm-fade"
    content-transition="vfm-fade"
    :click-to-close="auto"
    :esc-to-close="auto"
    :lock-scroll="true"
    :focus-trap="focusTrap"
    role="dialog"
    aria-modal="true"
    @update:model-value="(open: boolean) => emit('update:modelValue', open)"
    @before-open="page.hold()"
    @closed="page.release()"
    @before-close="emit('beforeClose')"
    @keydown="onKeydown"
    @mousedown="onPress"
    @mouseup="onRelease"
    @click.capture="holdLinks"
  >
    <slot v-if="auto" />
    <template v-else>
      <div ref="card" :class="cardClass" :inert="asking || undefined">
        <!-- Empty on purpose: the two bars of the cross are drawn by the
             stylesheet, and a glyph inside would stand behind them. -->
        <button
          ref="cross"
          type="button"
          class="dialog-close"
          aria-label="Закрыть"
          :aria-disabled="busy || undefined"
          @click="requestClose"
        ></button>
        <!-- `busy` for a body that holds a control of its own to shut while
             the request is in flight, as the warning window shuts its editor. -->
        <slot :busy="busy" />
      </div>
      <!-- Inside the frame and not teleported: the library's focus trap holds
           the caret within the window, and the question has to stand in it. -->
      <ConfirmDialog
        v-if="dirty || asking"
        inline
        :show="asking"
        title="Закрыть без сохранения?"
        :message="discardText"
        confirm-label="Закрыть"
        :action="leave"
        @update:show="(open: boolean) => (asking = open)"
      />
    </template>
  </vue-final-modal>
</template>

<style lang="sass">
@use "@/assets/styles/ZIndex" as *
@use "@/assets/styles/DialogSurface" as *

.vfm
  display: flex
  justify-content: center
  align-items: center
  // Align vfm modals with the self-rolled dialog tier (ConfirmDialog sits at
  // $z-dialog).
  z-index: $z-dialog

.dialog-overlay
  /* styled via ThemeVariables.css for proper cascade */

// The frame a site window stands in: the whole screen over the dimming, and
// the one thing that scrolls. A card taller than the screen starts at its top
// and scrolls whole, the cross and the buttons with the body, as a page does;
// nothing in it is sticky, because a sticky row covers the field that holds
// the caret. `margin: auto` on the card centres a card that fits and starts
// one that does not at the top, where centring would cut it at both ends.
.dialog-frame
  position: absolute
  inset: 0
  display: flex
  box-sizing: border-box
  padding: $medium
  overflow-y: auto
  overscroll-behavior: contain

// The card, with the width of a window that holds a form. One width for every
// form: a narrow tier of 380 made a window of four fields read as a column of
// scraps, and the owner removed it.
.dialog
  +dialog-card
  width: $dialog-width

  & h2
    margin-top: 0

.dialog-frame > .dialog
  margin: auto

.dialog--editor
  width: $dialog-width-editor

// The guest dialog's tier. AuthSurface.sass takes the wrapper over with
// `.dialog:has(> .auth-surface)`, which outranks both rules here, so this one
// only speaks for a browser without :has. It stays because AuthDialog passes
// `auto` and is not this window's to change.
.dialog--auto
  width: auto
  max-width: 320px

// Written under .dialog so the rule stays with this window: the confirmation
// and the editor's input window have a .dialog-close of their own.
.dialog > .dialog-close
  +dialog-close
</style>
