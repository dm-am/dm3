import { nextTick, onBeforeUnmount, watch, type Ref } from "vue";

/**
 * The behaviour every self-rolled modal owes its user: focus moves in when it
 * opens, cannot leave while it is open, comes back where it was on close, and
 * Escape closes.
 *
 * It exists because the two dialogs in this tier had drifted. ConfirmDialog
 * trapped Tab and restored focus; InputDialog did neither, so tabbing out of
 * it landed on the page behind the backdrop and the caret never came back to
 * whatever opened it. Copying the trap into the second one would have left two
 * copies to drift again.
 *
 * Enter is deliberately not handled here: one dialog submits a form on it and
 * the other confirms a destructive action only when focus is not on a button.
 * That is a per-dialog decision, not shell behaviour.
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

export function useDialogShell(options: DialogShellOptions) {
  let previouslyFocused: HTMLElement | null = null;

  watch(options.show, (show) => {
    if (show) {
      previouslyFocused = document.activeElement as HTMLElement | null;
      nextTick(() => options.initialFocus()?.focus());
    } else {
      previouslyFocused?.focus?.();
      previouslyFocused = null;
    }
  });

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
   */
  function handleKeydown(e: KeyboardEvent): boolean {
    if (e.key === "Escape") {
      e.preventDefault();
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

  /** Dismisses only when the click landed on the backdrop itself. */
  function handleBackdropClick(e: MouseEvent) {
    if (e.target === e.currentTarget) options.onDismiss();
  }

  return { handleKeydown, handleBackdropClick };
}
