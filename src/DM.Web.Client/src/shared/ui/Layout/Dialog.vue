<script setup lang="ts">
// The library closes nothing of a site window by itself: it reports neither Escape nor a click beside the
// card in a way that can be declined, so both go the cross's way here. The guest card (`auto`) keeps the library's.
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
    /** Open: from the library's container under useModal, or v-model. */
    modelValue?: boolean;
    /** Holds the BBCode editor: at 920px its toolbar and switch stand in one row. */
    editor?: boolean;
    /** The guest dialog (AuthDialog): its own card and cross, and no IN_DIALOG for what it holds. */
    auto?: boolean;
    /** What the main button of the form inside does: the window runs it, waits, refuses and closes. */
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

// The question goes with the window: closed first, the window takes the question's report with it.
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

/** The cross, Escape and "Отмена": nothing while busy, the question over typed text, else the way out. */
const requestClose = () => {
  if (busy.value) return;
  if (props.dirty) {
    asking.value = true;
    return;
  }
  leave();
};

/** A click beside the card never asks: too easy to make by accident; over typed text it does nothing. */
const closeBeside = () => {
  if (busy.value || props.dirty) return;
  leave();
};

// The session ended: the window goes, unless its own request is in flight and closes it through onExpired.
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

/** The caret's first place, focused here without a scroll: the trap scrolled a tall window and selected the field. */
const initialFocus = (): HTMLElement | undefined => {
  const root = card.value;
  if (!root) return undefined;
  const active = document.activeElement;
  if (active instanceof HTMLElement && root.contains(active)) return active;
  const first = fieldToFocus(root) ?? cross.value ?? undefined;
  first?.focus({ preventScroll: true });
  return first;
};

// Escape is the window's: the trap would switch itself off and hand the caret to the page.
const focusTrap = computed(() =>
  props.auto
    ? undefined
    : { allowOutsideClick: true, escapeDeactivates: false, initialFocus },
);

// The library's frame is no control: without this it is an invisible stop in the question's Tab cycle.
watch(card, (el) => el?.parentElement?.setAttribute("tabindex", "-1"));

// defaultPrevented is not read: ProseMirror prevents every Escape pressed in the editor.
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

/** No link leads away while the request is in flight: the answer would arrive nowhere. */
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
        <button
          ref="cross"
          type="button"
          class="dialog-close"
          aria-label="Закрыть"
          :aria-disabled="busy || undefined"
          @click="requestClose"
        ></button>
        <!-- `busy` for a body that shuts a control of its own, as the warning window shuts its editor. -->
        <slot :busy="busy" />
      </div>
      <!-- Inside the frame: the library's focus trap holds the caret within the window. -->
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
  // The tier of ConfirmDialog.
  z-index: $z-dialog

// The one thing that scrolls; nothing in the card is sticky: a sticky row covers the field with the caret.
.dialog-frame
  position: absolute
  inset: 0
  display: flex
  box-sizing: border-box
  padding: $medium
  overflow-y: auto
  overscroll-behavior: contain

// One width for every form: the owner removed the narrow tier.
.dialog
  +dialog-card
  width: $dialog-width

  & h2
    margin-top: 0

.dialog-frame > .dialog
  margin: auto

.dialog--editor
  width: $dialog-width-editor

// The guest dialog's tier. _AuthCard.sass takes the wrapper over with
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
