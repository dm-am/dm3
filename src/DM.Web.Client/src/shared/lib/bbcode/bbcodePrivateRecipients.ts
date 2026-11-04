import {
  anchoredTooltipOwner,
  hideAnchoredTooltip,
  showAnchoredTooltip,
} from "@/shared/ui/Tooltip";
import { registerCleanup } from "./bbcodeCleanup";

/** Longer than a control's: a reader rests the pointer on the text he reads. */
export const PRIVATE_HOVER_DELAY = 600;

/** The grace the bubble keeps while the pointer crosses between two lines. */
const PRIVATE_LEAVE_DELAY = 150;

/** The leads, whom the readers of every hidden block start with (PrivateRecipients on the server). */
const PRIVATE_LEADS = ["DungeonMaster", "Assistant"];

/** The words after "Получатели:" of a hidden block or a marked file: the leads, then the names. */
export function privateRecipientsLine(names: readonly string[]): string {
  return [...PRIVATE_LEADS, ...names].join(", ");
}

/** "Получатели: DungeonMaster, Assistant, Вася", the one wording of who reads hidden text. */
export function recipientsCaption(line: string): string {
  return `Получатели: ${line}`;
}

/**
 * The words of the tooltip over private text: "Получатели: DungeonMaster,
 * Assistant, Вася", from the block's data-recipients, leads first (PrivateTag);
 * a line in the post stretched the gaps of a justified paragraph. Shown as
 * text: the names are the players' own.
 */
export function privateRecipientsText(piece: Element): string | null {
  const names = (piece.getAttribute("data-recipients") ?? "")
    .replace(/\s+/g, " ")
    .trim();
  return names ? recipientsCaption(names) : null;
}

/** The line of `element` nearest to the point, as an index into its client rectangles. */
function lineIndexAt(element: Element, x: number, y: number): number {
  let best = 0;
  let bestDistance = Number.POSITIVE_INFINITY;
  Array.from(element.getClientRects()).forEach((rect, index) => {
    const dy = y < rect.top ? rect.top - y : Math.max(0, y - rect.bottom);
    const dx = x < rect.left ? rect.left - x : Math.max(0, x - rect.right);
    // Vertical distance first: the line, then the place on it.
    const distance = dy * 100_000 + dx;
    if (distance < bestDistance) {
      bestDistance = distance;
      best = index;
    }
  });
  return best;
}

/** On that line, under the pointer, not centred on the whole line. */
function lineAnchor(
  element: Element,
  index: number,
  x: number,
): () => DOMRect | null {
  return () => {
    if (!element.isConnected) return null;
    const line =
      element.getClientRects()[index] ?? element.getBoundingClientRect();
    const at = Math.min(Math.max(x, line.left), line.right);
    return new DOMRect(at, line.top, 0, line.height);
  };
}

/** The one block whose bubble is up, and what closes it. */
let recipientsOpen: { piece: HTMLElement } | null = null;
let recipientsShowTimer: ReturnType<typeof setTimeout> | null = null;
let recipientsHideTimer: ReturnType<typeof setTimeout> | null = null;

function clearRecipientsTimers(): void {
  for (const timer of [recipientsShowTimer, recipientsHideTimer]) {
    if (timer) clearTimeout(timer);
  }
  recipientsShowTimer = null;
  recipientsHideTimer = null;
}

function hideRecipients(): void {
  clearRecipientsTimers();
  if (recipientsOpen) hideAnchoredTooltip(recipientsOpen.piece);
  recipientsOpen = null;
  listenWhileRecipientsOpen(false);
}

function showRecipients(
  piece: HTMLElement,
  text: string,
  point: { x: number; y: number },
): void {
  clearRecipientsTimers();
  showAnchoredTooltip({
    text,
    owner: piece,
    anchor: lineAnchor(piece, lineIndexAt(piece, point.x, point.y), point.x),
  });
  recipientsOpen = { piece };
  listenWhileRecipientsOpen(true);
}

// A press outside the block, a scroll, a key or a resize takes the bubble
// away; a press inside is the block's own to decide.
function onRecipientsDocumentPointerDown(event: Event): void {
  const open = recipientsOpen;
  if (!open) return;
  const target = event.target as Node | null;
  if (target && open.piece.contains(target)) return;
  hideRecipients();
}

function onRecipientsDismiss(): void {
  if (recipientsOpen) hideRecipients();
}

function listenWhileRecipientsOpen(on: boolean): void {
  const method = on ? "addEventListener" : "removeEventListener";
  document[method]("pointerdown", onRecipientsDocumentPointerDown, true);
  document[method]("keydown", onRecipientsDismiss, true);
  window[method]("scroll", onRecipientsDismiss, true);
  window[method]("resize", onRecipientsDismiss);
}

/** Blocks already listening, so that a second init adds nothing. */
const privateListeners = new WeakSet<HTMLElement>();

/** The mouse over private text: hover, move, leave and a press that starts a selection. Returns the cleanup. */
function hearMouse(piece: HTMLElement, text: string): () => void {
  let point = { x: 0, y: 0 };

  // A mouse button held over the block is a selection being made, begun on
  // the block or dragged into it: the bubble keeps away until the pointer
  // leaves and comes back with the button up.
  const keepAway = (): void => {
    if (recipientsOpen?.piece === piece) hideRecipients();
    else if (recipientsShowTimer) {
      clearTimeout(recipientsShowTimer);
      recipientsShowTimer = null;
    }
  };

  const onMouseEnter = (event: MouseEvent): void => {
    point = { x: event.clientX, y: event.clientY };
    if (recipientsOpen?.piece === piece) {
      // Back across the gap between two lines before the grace ran out.
      if (recipientsHideTimer) clearTimeout(recipientsHideTimer);
      recipientsHideTimer = null;
      return;
    }
    if (event.buttons !== 0) return;
    if (recipientsShowTimer) clearTimeout(recipientsShowTimer);
    recipientsShowTimer = setTimeout(
      () => showRecipients(piece, text, point),
      PRIVATE_HOVER_DELAY,
    );
  };

  const onMouseMove = (event: MouseEvent): void => {
    point = { x: event.clientX, y: event.clientY };
    if (event.buttons !== 0) keepAway();
  };

  const onMouseLeave = (): void => {
    if (recipientsOpen?.piece === piece) {
      if (recipientsHideTimer) clearTimeout(recipientsHideTimer);
      recipientsHideTimer = setTimeout(hideRecipients, PRIVATE_LEAVE_DELAY);
      return;
    }
    if (recipientsShowTimer) clearTimeout(recipientsShowTimer);
    recipientsShowTimer = null;
  };

  const onMouseDown = (): void => {
    keepAway();
  };

  piece.addEventListener("mouseenter", onMouseEnter);
  piece.addEventListener("mousemove", onMouseMove);
  piece.addEventListener("mouseleave", onMouseLeave);
  piece.addEventListener("mousedown", onMouseDown);

  return () => {
    piece.removeEventListener("mouseenter", onMouseEnter);
    piece.removeEventListener("mousemove", onMouseMove);
    piece.removeEventListener("mouseleave", onMouseLeave);
    piece.removeEventListener("mousedown", onMouseDown);
  };
}

/**
 * Who reads private text, in a tooltip over the line under the pointer: after
 * PRIVATE_HOVER_DELAY, gone on leave, kept away while a selection is made.
 * Keyboard: nothing, or every green block would take a Tab stop. The bubble lives
 * outside the text, so a selection copies the text alone.
 */
export function initPrivateRecipients(container: HTMLElement): void {
  const pieces = container.querySelectorAll<HTMLElement>(".private-message");
  const cleanupFunctions: Array<() => void> = [];

  pieces.forEach((piece) => {
    if (privateListeners.has(piece)) return;
    const text = privateRecipientsText(piece);
    if (!text) return;

    privateListeners.add(piece);

    const unhearMouse = hearMouse(piece, text);

    cleanupFunctions.push(() => {
      unhearMouse();
      privateListeners.delete(piece);
      if (anchoredTooltipOwner() === piece) hideRecipients();
    });
  });

  if (cleanupFunctions.length > 0) {
    registerCleanup(container, () => {
      cleanupFunctions.forEach((fn) => fn());
    });
  }
}
