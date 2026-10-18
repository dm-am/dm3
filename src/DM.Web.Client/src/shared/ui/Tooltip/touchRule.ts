/**
 * How a finger meets a tooltip, one rule for every trigger on the site: the
 * Tooltip component, the terms inside a tooltip (TooltipContent) and the hidden
 * text of a post (bbcodeInteractive).
 *
 * A tap does what a click does, and it opens a tooltip only where the tooltip
 * tells something and the tap does not take the reader away:
 *
 * - Text, a number, a date, an icon, a disabled control: a short tap opens the
 *   tooltip. A second tap, a tap anywhere else or TAP_CLOSE_DELAY close it; the
 *   delay only for a one-line bubble with nothing in it to press, since a list
 *   of names cannot be read in two and a half seconds and a link inside has to
 *   stay long enough to be reached.
 * - A link: the tap follows it, and no bubble flashes on the way.
 * - An enabled button: the tap does what the button does. A tooltip that only
 *   names the button stays shut, the name is already the button's own for a
 *   screen reader. A button whose tooltip carries data and that leaves the
 *   reader on the page (a tag in the picker, a like) opts in with `showOnTap`.
 * - A scroll, the tap that stops a fling and a long press open nothing. The
 *   tap is read from `click`, which the browser sends for none of the three,
 *   and only when the finger came up where it went down and soon (isTap): a
 *   browser that does end a long press with a click still gets no bubble for
 *   it. The long press stays the phone's: selection, copy, the link menu.
 * - Hidden text of a post, the one place where the bubble goes first: its
 *   first tap opens "Получатели: ..." wherever it lands, on a link, a spoiler
 *   or a picture inside the text too, and does nothing else; while the bubble
 *   is open, a tap on the link follows it, on the spoiler opens it, on the
 *   picture opens it whole. A hidden text that is one link would otherwise
 *   never say on a phone whom it is for, since the bubble is all that says it
 *   (bbcodeInteractive, initPrivateRecipients).
 *
 * The mouse and the keyboard work as they did: hover after a delay, focus from
 * the keyboard at once. What changes for them is only that the mouse events a
 * browser makes up after a tap no longer count as hover, and a focus that a
 * tap or a click gave a trigger no longer counts as keyboard focus.
 */

/** How long a bubble opened by a tap stays when nobody closes it. */
export const TAP_CLOSE_DELAY = 2500;

/** A tap comes up within this distance of where it went down... */
export const TAP_SLOP = 10;

/** ...and this soon after. Longer is a long press. */
export const TAP_MAX_DURATION = 500;

export type InputKind = "keyboard" | "mouse" | "touch" | "pen";

let lastInput: InputKind | null = null;
let tracking = false;

/** The last press of a finger: where and when it went down and came up. */
interface Press {
  x: number;
  y: number;
  downAt: number;
  upAt: number | null;
  distance: number;
  cancelled: boolean;
}

let press: Press | null = null;

const isFinger = (kind: string | null | undefined): boolean =>
  kind === "touch" || kind === "pen";

function onPointer(event: Event): void {
  const type = (event as Partial<PointerEvent>).pointerType;
  if (type === "mouse" || type === "touch" || type === "pen") lastInput = type;
}

function onPointerDown(event: Event): void {
  onPointer(event);
  const pointer = event as PointerEvent;
  press = isFinger(pointer.pointerType)
    ? {
        x: pointer.clientX,
        y: pointer.clientY,
        downAt: pointer.timeStamp,
        upAt: null,
        distance: 0,
        cancelled: false,
      }
    : null;
}

function onPointerUp(event: Event): void {
  const pointer = event as PointerEvent;
  if (!press || !isFinger(pointer.pointerType)) return;
  press.upAt = pointer.timeStamp;
  press.distance = Math.hypot(
    pointer.clientX - press.x,
    pointer.clientY - press.y,
  );
}

// The browser took the gesture over: a scroll, a pinch, a long press.
function onPointerCancel(): void {
  if (press) press.cancelled = true;
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
 * over a trigger has to count as a mouse before the trigger's `mouseenter`
 * arrives.
 */
export function trackInput(): void {
  if (tracking || typeof document === "undefined") return;
  tracking = true;
  const options = { capture: true, passive: true } as const;
  document.addEventListener("pointerdown", onPointerDown, options);
  document.addEventListener("pointerup", onPointerUp, options);
  document.addEventListener("pointercancel", onPointerCancel, options);
  document.addEventListener("pointerover", onPointer, options);
  document.addEventListener("keydown", onKey, options);
}

/** The input the reader used last, or null before any. */
export function lastInputKind(): InputKind | null {
  return lastInput;
}

/**
 * Mouse events arriving now were made up by the browser after a touch.
 *
 * After a tap the browser sends mouseover, mouseenter, mousedown, mouseup and
 * the click. Read as hover they opened the bubble a second time, after the
 * delay, whatever the tap itself had decided.
 */
export function isCompatMouseEvent(): boolean {
  return isFinger(lastInput);
}

/**
 * The event comes from a finger or a pen. A pointer event names its own
 * pointer. A click does not, reliably: WebKit sends the click of a tap with
 * pointerType "mouse" (seen under touch emulation), and a keyboard's click has
 * none. For those the pointer that went down last tells, heard on the document
 * before any trigger hears the click.
 */
export function isTouchEvent(event: Event): boolean {
  if (event.type.startsWith("pointer")) {
    return isFinger((event as Partial<PointerEvent>).pointerType);
  }
  return isFinger(lastInput);
}

/**
 * The finger that went down last came up where it went down, and soon: a tap,
 * not a long press and not a drag. Read at pointerup and at the click after it.
 */
export function isShortPress(): boolean {
  return (
    press !== null &&
    !press.cancelled &&
    press.upAt !== null &&
    press.upAt - press.downAt <= TAP_MAX_DURATION &&
    press.distance <= TAP_SLOP
  );
}

/** A click that is a tap: a finger's, and a short press (isShortPress). */
export function isTap(event: Event): boolean {
  return isTouchEvent(event) && isShortPress();
}

/**
 * A focus that came from the keyboard, the only focus that opens a tooltip by
 * itself. A tap or a click focuses the control under it too, and that focus
 * held the bubble open past every rule of the tap.
 *
 * Two signs, either one enough. The browser's own: the focused element
 * matches `:focus-visible`, which it does not after a tap or a click on a
 * button or on text, and does when the focus came from the keyboard or from a
 * screen reader that moves it without a key reaching the page. And the input
 * heard last: a key, or nothing at all yet (a focus the page set on load).
 */
export function isKeyboardFocus(focused?: EventTarget | null): boolean {
  if (lastInput === "keyboard" || lastInput === null) return true;
  if (!(focused instanceof Element)) return false;
  try {
    return focused.matches(":focus-visible");
  } catch {
    // An engine that does not know the selector: the input heard last decides.
    return false;
  }
}

/** What a tap on `target` does besides opening a tooltip. */
export type TapTarget = "link" | "control" | "text";

const INTERACTIVE = [
  "a[href]",
  "area[href]",
  "button",
  "input",
  "select",
  "textarea",
  "summary",
  "label",
  '[role="button"]',
  '[role="link"]',
  '[role="checkbox"]',
  '[role="switch"]',
  '[role="tab"]',
  '[role="menuitem"]',
  '[contenteditable=""]',
  '[contenteditable="true"]',
].join(", ");

const isDisabled = (element: Element): boolean =>
  element.matches(":disabled") ||
  element.getAttribute("aria-disabled") === "true";

/**
 * The nearest control the tap lands in, inside the trigger or around it: a
 * trigger inside a link is a link to the finger all the same. A disabled
 * control and a label of one are text: the tap does nothing there, and the
 * tooltip is what says why.
 */
export function tapTargetOf(target: EventTarget | null): TapTarget {
  if (!(target instanceof Element)) return "text";
  const control = target.closest(INTERACTIVE);
  if (!control) return "text";
  if (control.matches('a[href], area[href], [role="link"]')) return "link";
  if (isDisabled(control)) return "text";
  if (control instanceof HTMLLabelElement) {
    const labelled = control.control;
    return labelled && !isDisabled(labelled) ? "control" : "text";
  }
  return "control";
}

/** The disabled control under `target` inside `within`, if any. */
export function disabledControlOf(
  target: EventTarget | null,
  within: Element,
): Element | null {
  if (!(target instanceof Element)) return null;
  const control = target.closest("button, input, select, textarea");
  return control && within.contains(control) && control.matches(":disabled")
    ? control
    : null;
}

/** Something in a bubble a finger has to be able to reach. */
const REACHABLE_IN_BUBBLE =
  "a[href], button, input, select, textarea, [tabindex], .bb-tip";

/** The bubble holds something to press: a link, a button, a term. */
export function bubbleHasControls(bubble: Element): boolean {
  return bubble.querySelector(REACHABLE_IN_BUBBLE) !== null;
}

/**
 * A bubble opened by a tap closes by itself only while it is one line of plain
 * text. `text` is the inline element that holds the words: an inline box has
 * one client rectangle per line it spans.
 */
export function closesByItself(bubble: Element, text: Element | null): boolean {
  if (bubbleHasControls(bubble)) return false;
  return (text ?? bubble).getClientRects().length <= 1;
}
