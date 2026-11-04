// Which input the reader used last, the keyboard or a pointer: a focus opens a tooltip only when the keyboard
// gave it, and a click focuses the control under it too.

type InputKind = "keyboard" | "pointer";

let lastInput: InputKind | null = null;
let tracking = false;

function onPointer(): void {
  lastInput = "pointer";
}

function onKey(event: Event): void {
  const key = event as KeyboardEvent;
  // A modifier held for a shortcut is not a move of the keyboard focus.
  if (key.metaKey || key.altKey || key.ctrlKey) return;
  lastInput = "keyboard";
}

/**
 * Starts listening for the kind of input the reader uses, once per page.
 *
 * On `document`, in the capture phase, so it hears the input before any
 * trigger does. `pointerover` as well as `pointerdown`: a mouse that only moves
 * over a trigger counts as a pointer before the trigger's `mouseenter` arrives.
 */
export function trackInput(): void {
  if (tracking) return;
  tracking = true;
  const options = { capture: true, passive: true } as const;
  document.addEventListener("pointerdown", onPointer, options);
  document.addEventListener("pointerover", onPointer, options);
  document.addEventListener("keydown", onKey, options);
}

/**
 * A focus that came from the keyboard, the only focus that opens a tooltip by
 * itself. A click focuses the control under it too, and that focus held the
 * bubble open past the pointer leaving.
 *
 * Two signs, either one enough. The browser's own: the focused element
 * matches `:focus-visible`, which it does not after a click on a button or on
 * text, and does when the focus came from the keyboard or from a screen reader
 * that moves it without a key reaching the page. And the input heard last: a
 * key, or nothing at all yet (a focus the page set on load).
 */
export function isKeyboardFocus(focused?: EventTarget | null): boolean {
  if (lastInput === "keyboard" || lastInput === null) return true;
  if (!(focused instanceof Element)) return false;
  return focused.matches(":focus-visible");
}
