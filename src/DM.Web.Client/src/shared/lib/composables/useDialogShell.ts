import { nextTick, onBeforeUnmount, watch, type Ref } from "vue";
import { usePageScrollLock } from "./usePageScrollLock";
import { useOpenWindow } from "./useOpenWindows";

/**
 * What every self-rolled modal owes its user: focus moves in, cannot leave,
 * comes back on close, and Escape closes. Enter is the form's and the button's.
 * While open it holds the page still (usePageScrollLock), and it closes with
 * the session (useOpenWindows).
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
 * Where the caret goes in a window with a form: the first empty field, else the
 * first field, else null. A list always holds a value and is not a field here.
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
  const page = usePageScrollLock();
  useOpenWindow(options.show, options.onDismiss);

  // Read at once: the window under a question turns inert in the same render.
  watch(
    options.show,
    (show) => {
      if (show)
        previouslyFocused = document.activeElement as HTMLElement | null;
    },
    { immediate: options.show.value, flush: "sync" },
  );

  // After the render, without a scroll: a tall dialog opens at its top.
  watch(
    options.show,
    (show) => {
      if (show) {
        page.hold();
        nextTick(() => options.initialFocus()?.focus({ preventScroll: true }));
      } else {
        page.release();
        previouslyFocused?.focus?.();
        previouslyFocused = null;
      }
    },
    { immediate: options.show.value, flush: "post" },
  );

  // Unmounting while open is closing, for the caret.
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
   * Handles Escape and the Tab trap; true when the event was consumed. Escape
   * stops at the dialog that took it, not the window under it.
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

  /** Whether the latest press began on the backdrop; unknown until reported. */
  let pressedOnBackdrop: boolean | null = null;

  /**
   * A drag out of the card released over the backdrop reads as a backdrop
   * click, and the backdrop's own scrollbar takes presses too.
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
