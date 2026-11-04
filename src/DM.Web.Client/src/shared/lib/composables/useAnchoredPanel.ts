import { nextTick, onBeforeUnmount, onMounted, type Ref } from "vue";
import { fieldToFocus } from "./useDialogShell";

/**
 * A small panel hung under the control that opened it: an insertion of the
 * editor, who sees a file of a post. It takes the focus when it opens; Escape
 * closes it and gives the focus back to the control (`back`), and a press or
 * a Tab outside closes it where the reader went. A press on the control itself
 * is the control's: it opens and closes the panel.
 *
 * Never carried to <body>: a window's focus trap would take the caret back.
 */
export function useAnchoredPanel(
  panel: Ref<HTMLElement | null>,
  anchor: () => HTMLElement | null,
  close: (back: boolean) => void,
) {
  const isOwn = (target: EventTarget | null): boolean =>
    target instanceof Node &&
    (!!panel.value?.contains(target) || !!anchor()?.contains(target));

  function onPressOutside(event: Event) {
    if (!isOwn(event.target)) close(false);
  }

  // Tab out of the panel closes it: the reader has gone on to the page.
  function onFocusOut(event: FocusEvent) {
    const next = event.relatedTarget;
    if (next && !isOwn(next)) close(false);
  }

  /** True when the key was Escape and the panel has closed on it. */
  function closedByEscape(event: KeyboardEvent): boolean {
    if (event.key !== "Escape") return false;
    // The panel is the top layer: a window it stands in stays open.
    event.preventDefault();
    event.stopPropagation();
    close(true);
    return true;
  }

  onMounted(() => {
    nextTick(() => {
      if (panel.value) (fieldToFocus(panel.value) ?? panel.value).focus();
    });
    document.addEventListener("pointerdown", onPressOutside, true);
  });

  onBeforeUnmount(() => {
    document.removeEventListener("pointerdown", onPressOutside, true);
  });

  return { onFocusOut, closedByEscape };
}
