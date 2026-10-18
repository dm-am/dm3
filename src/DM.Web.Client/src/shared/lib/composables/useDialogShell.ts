import { nextTick, onBeforeUnmount, watch, type Ref } from "vue";
import { usePageScrollLock } from "./usePageScrollLock";
import { useOpenWindow } from "./useOpenWindows";

/**
 * The behaviour every self-rolled modal owes its user: focus moves in when it
 * opens, cannot leave while it is open, comes back where it was on close, and
 * Escape closes.
 *
 * It exists because the two dialogs in this tier had drifted. ConfirmDialog
 * trapped Tab and restored focus; the insert window of the markup editor did
 * neither, so tabbing out of it landed on the page behind the backdrop and the
 * caret never came back to whatever opened it. Copying the trap into the second one would have left two
 * copies to drift again.
 *
 * Enter is deliberately not handled here. A form submits on it from its own
 * field, which is the browser's behaviour and the dialog's to keep, and a
 * button that holds the focus answers Enter itself.
 */
export interface DialogShellOptions {
  /** Whether the dialog is currently open. */
  show: Ref<boolean>;
  /** The element that owns the dialog's focusable content. */
  container: Ref<HTMLElement | null>;
  /** What to focus once the dialog is on screen. */
  initialFocus: () => HTMLElement | null | undefined;
  /** Called on Escape and on a click outside the container. */
  onDismiss: () => void;
  /**
   * Holds the page still while the dialog is open (usePageScrollLock). A
   * window says so; the side column of a narrow screen (MobileDrawer) takes
   * usePageScrollLock itself, since it is not a window.
   *
   * A window is also closed with the session (useOpenWindows), the way
   * Escape closes it. The side column is not a window and stays.
   */
  holdPage?: boolean;
}

/** The fields a reader types into, in the order a form lists them. */
const TYPED =
  'input:not([type="hidden"]):not([type="checkbox"]):not([type="radio"]):not([type="button"]):not([type="submit"]), textarea, [contenteditable="true"]';

/** v-show hides by inline style, and a hidden field is not the one to type in. */
const isHidden = (el: HTMLElement, stop: ParentNode): boolean => {
  for (let node: HTMLElement | null = el; node; node = node.parentElement) {
    if (node.hidden || node.style.display === "none") return true;
    if (node === stop) break;
  }
  return false;
};

const isEmpty = (el: HTMLElement): boolean =>
  el instanceof HTMLInputElement || el instanceof HTMLTextAreaElement
    ? !el.value
    : !el.textContent?.trim();

/**
 * Where the caret goes in a window that holds a form: the first field still
 * empty, so a form that arrives with its address filled in starts on the
 * password; the first field when every one is filled; null when there is none.
 * A list is not a field here: it always holds a value, and the reader who
 * opened the window came to type.
 */
export function fieldToFocus(root: ParentNode): HTMLElement | null {
  const fields = Array.from(root.querySelectorAll<HTMLElement>(TYPED)).filter(
    (el) =>
      !el.hasAttribute("disabled") &&
      !el.closest("[inert]") &&
      !isHidden(el, root),
  );
  return fields.find(isEmpty) ?? fields[0] ?? null;
}

export function useDialogShell(options: DialogShellOptions) {
  let previouslyFocused: HTMLElement | null = null;
  const page = options.holdPage ? usePageScrollLock() : null;
  if (options.holdPage) useOpenWindow(options.show, options.onDismiss);

  // Where the caret was is read at once, before anything renders: the window
  // under a question turns inert in the same render that shows the question,
  // and an inert element lets go of the focus it held.
  watch(
    options.show,
    (show) => {
      if (show)
        previouslyFocused = document.activeElement as HTMLElement | null;
    },
    { immediate: options.show.value, flush: "sync" },
  );

  // Given back after the render, so the caret returns to a page that is there
  // again: the window under the question takes focus once it is not inert.
  // The first place is taken without a scroll, so a dialog taller than the
  // screen opens at its top and not at the control it focuses.
  watch(
    options.show,
    (show) => {
      if (show) {
        page?.hold();
        nextTick(() => options.initialFocus()?.focus({ preventScroll: true }));
      } else {
        page?.release();
        previouslyFocused?.focus?.();
        previouslyFocused = null;
      }
    },
    { immediate: options.show.value, flush: "post" },
  );

  // Unmounting while open is the same event as closing, as far as the caret is
  // concerned: a route change must not leave focus on a detached node.
  onBeforeUnmount(() => previouslyFocused?.focus?.());

  /** Focusable elements inside the dialog, in DOM order. */
  function focusables(): HTMLElement[] {
    if (!options.container.value) return [];
    return Array.from(
      options.container.value.querySelectorAll<HTMLElement>(
        'button, [href], input, select, textarea, [tabindex]:not([tabindex="-1"])',
      ),
    ).filter((el) => !el.hasAttribute("disabled"));
  }

  /**
   * Handles Escape and the Tab trap. Returns true when the event was consumed,
   * so a caller can keep its own handling for the rest.
   *
   * Escape goes no further than the dialog that took it. A dialog can stand
   * inside a window (the question over a window with typed text), and the
   * press that answers the question would otherwise reach the window under it
   * and ask the question again.
   */
  function handleKeydown(e: KeyboardEvent): boolean {
    if (e.key === "Escape") {
      e.preventDefault();
      e.stopPropagation();
      options.onDismiss();
      return true;
    }

    if (e.key !== "Tab") return false;

    const items = focusables();
    if (!items.length) return false;

    const first = items[0];
    const last = items[items.length - 1];
    const active = document.activeElement as HTMLElement | null;

    if (e.shiftKey && active === first) {
      e.preventDefault();
      last.focus();
      return true;
    }
    if (!e.shiftKey && active === last) {
      e.preventDefault();
      first.focus();
      return true;
    }
    return false;
  }

  /**
   * Whether the latest press began on the backdrop itself. Unknown until a
   * caller reports presses: the drawer reports none and keeps the plain rule.
   */
  let pressedOnBackdrop: boolean | null = null;

  /**
   * Records where a press began. A click is dispatched to the common ancestor
   * of the press and the release, so a selection dragged out of the card and
   * let go over the backdrop reads as a click on the backdrop; and a backdrop
   * that scrolls a tall card takes presses on its own scrollbar.
   */
  function handleBackdropPress(e: MouseEvent) {
    const backdrop = e.currentTarget as HTMLElement;
    const onScrollbar =
      backdrop.clientWidth > 0 && e.offsetX >= backdrop.clientWidth;
    pressedOnBackdrop = e.target === backdrop && !onScrollbar;
  }

  /** Dismisses only when the click landed on the backdrop itself. */
  function handleBackdropClick(e: MouseEvent) {
    if (e.target !== e.currentTarget) return;
    if (pressedOnBackdrop === false) return;
    options.onDismiss();
  }

  return { handleKeydown, handleBackdropPress, handleBackdropClick };
}
