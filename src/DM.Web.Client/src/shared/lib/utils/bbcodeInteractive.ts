/**
 * BBCode Interactive Elements
 *
 * Shared utilities for initializing interactive BBCode elements (spoilers, NSFW,
 * pictures that open whole, the tooltip naming who reads a hidden block)
 * in server-rendered content. Used by every component that displays
 * server-rendered BBCode content.
 *
 * This ensures consistent behavior across all places where BBCode is displayed.
 *
 * Features:
 * - Event listener cleanup to prevent memory leaks
 * - WCAG 2.1 keyboard navigation (Enter/Space to toggle)
 * - ARIA attributes for screen reader support
 */

import {
  SPOILER_SHOW_TEXT,
  SPOILER_HIDE_TEXT,
  NSFW_SHOW_TEXT,
  NSFW_HIDE_TEXT,
  NSFW_WARNING_TEXT,
} from "./bbcodeConstants";
import {
  registerExpandable,
  notifyExpandableChanged,
} from "@/shared/lib/composables/useExpandableRegistry";
import { openImageViewer } from "@/shared/lib/composables/useImageViewer";
import {
  TAP_CLOSE_DELAY,
  anchoredTooltipElement,
  anchoredTooltipOwner,
  closesByItself,
  hideAnchoredTooltip,
  isCompatMouseEvent,
  isTap,
  showAnchoredTooltip,
  tapTargetOf,
  trackInput,
} from "@/shared/ui/Tooltip";

// ============================================================================
// CLEANUP TRACKING
// ============================================================================

/**
 * WeakMap to store cleanup functions for each container.
 * WeakMap allows garbage collection when containers are removed from DOM.
 */
const cleanupMap = new WeakMap<HTMLElement, () => void>();

/**
 * Store a cleanup function for a container
 */
function registerCleanup(container: HTMLElement, cleanup: () => void): void {
  const existing = cleanupMap.get(container);
  if (existing) {
    // Combine with existing cleanup
    cleanupMap.set(container, () => {
      existing();
      cleanup();
    });
  } else {
    cleanupMap.set(container, cleanup);
  }
}

/**
 * Cleanup all event listeners for a container.
 * Call this in Vue's onBeforeUnmount or when content changes.
 */
export function cleanupBbcodeInteractive(container: HTMLElement | null): void {
  if (!container) return;

  const cleanup = cleanupMap.get(container);
  if (cleanup) {
    cleanup();
    cleanupMap.delete(container);
  }

  // Also cleanup data-initialized markers so elements can be re-initialized
  container.querySelectorAll("[data-initialized]").forEach((el) => {
    el.removeAttribute("data-initialized");
  });
}

// ============================================================================
// HELPER FUNCTIONS
// ============================================================================

/**
 * Shared predicate: is an element node empty whitespace for trimming purposes?
 * Empty means: <br>, or an empty <p>/<div> without any media children.
 */
function isTrimmableEmptyElement(elem: Element): boolean {
  const tagName = elem.tagName.toUpperCase();
  if (tagName === "BR") return true;
  if (tagName === "P" || tagName === "DIV") {
    return (
      elem.textContent?.trim() === "" &&
      !elem.querySelector("img, iframe, video, audio, svg")
    );
  }
  return false;
}

/**
 * Pure HTML string transform: strips leading and trailing "empty" content
 * (<br>, empty <p>/<div>, whitespace-only text nodes) symmetrically from
 * either end of an HTML fragment. Mid-content empty lines are preserved
 * (author intent).
 *
 * Uses a detached <template> element for parsing — no interaction with
 * the live DOM, no Vue reactivity side-effects. Callers pre-transform
 * their HTML in a computed before passing to v-html.
 */
export function trimHtmlWhitespace(html: string | null | undefined): string {
  if (!html) return "";
  const template = document.createElement("template");
  template.innerHTML = html;
  trimLeadingInFragment(template.content);
  trimTrailingInFragment(template.content);
  return template.innerHTML;
}

function trimLeadingInFragment(root: DocumentFragment | Element): void {
  while (root.firstChild) {
    const first = root.firstChild;
    if (first.nodeType === Node.TEXT_NODE) {
      if ((first.textContent || "").trim() === "") {
        first.remove();
        continue;
      }
      first.textContent = (first.textContent || "").trimStart();
      break;
    }
    if (first.nodeType === Node.ELEMENT_NODE) {
      const elem = first as Element;
      if (isTrimmableEmptyElement(elem)) {
        elem.remove();
        continue;
      }
      trimLeadingInFragment(elem);
      break;
    }
    break;
  }
}

function trimTrailingInFragment(root: DocumentFragment | Element): void {
  while (root.lastChild) {
    const last = root.lastChild;
    if (last.nodeType === Node.TEXT_NODE) {
      if ((last.textContent || "").trim() === "") {
        last.remove();
        continue;
      }
      last.textContent = (last.textContent || "").trimEnd();
      break;
    }
    if (last.nodeType === Node.ELEMENT_NODE) {
      const elem = last as Element;
      if (isTrimmableEmptyElement(elem)) {
        elem.remove();
        continue;
      }
      trimTrailingInFragment(elem);
      break;
    }
    break;
  }
}

/**
 * Remove unnecessary <br> elements around BBCode block elements.
 *
 * The BBCode parser converts newlines to <br>, but block elements
 * (.spoiler-head, .nsfw-head, .spoiler, .nsfw-spoiler) create their own
 * line breaks. Having <br> adjacent to them creates double line breaks.
 */
function removeBrAroundBlockElements(container: HTMLElement | null): void {
  if (!container) return;

  const blockSelectors = [
    ".spoiler-head",
    ".nsfw-head",
    ".spoiler",
    ".nsfw-spoiler",
  ];

  blockSelectors.forEach((selector) => {
    container.querySelectorAll(selector).forEach((el) => {
      // Remove <br> immediately before
      const prev = el.previousSibling;
      if (prev && prev.nodeName === "BR") {
        prev.remove();
      }

      // Remove <br> immediately after
      const next = el.nextSibling;
      if (next && next.nodeName === "BR") {
        next.remove();
      }
    });
  });
}

/**
 * Handle keyboard events for toggle elements (WCAG 2.1 compliance)
 */
function handleKeyboardToggle(
  e: KeyboardEvent,
  clickHandler: () => void,
): void {
  if (e.key === "Enter" || e.key === " ") {
    e.preventDefault();
    clickHandler();
  }
}

// ============================================================================
// SPOILER INITIALIZATION
// ============================================================================

/**
 * Initialize spoiler toggle behavior for server-rendered spoilers.
 * Server structure: <a class="spoiler-head">...</a><div class="spoiler">...</div>
 *
 * The .spoiler is wrapped into the shared collapse structure so expand and
 * collapse animate smoothly (grid-template-rows 0fr/1fr, see the .bb-collapse
 * rules in _BbcodeContent.sass):
 *
 *   <a class="spoiler-head">
 *   <div class="bb-collapse [open]">
 *     <div class="bb-collapse-clip">
 *       <div class="spoiler">...</div>
 *
 * WCAG features:
 * - role="button" for screen readers
 * - tabindex="0" for keyboard focus
 * - aria-expanded to indicate state
 * - Enter/Space keyboard support
 * - inert on the collapsed wrapper keeps hidden content out of the tab
 *   order and the accessibility tree (parity with the old display: none)
 */
export function initSpoilers(container: HTMLElement | null): void {
  if (!container) {
    if (import.meta.env.DEV) {
      console.warn(
        "[bbcodeInteractive] initSpoilers called with null container",
      );
    }
    return;
  }

  const spoilerHeads = container.querySelectorAll(
    ".spoiler-head:not([data-initialized])",
  );
  const cleanupFunctions: Array<() => void> = [];

  if (import.meta.env.DEV && spoilerHeads.length > 0) {
    console.debug(
      `[bbcodeInteractive] Found ${spoilerHeads.length} spoiler-head elements`,
    );
  }

  spoilerHeads.forEach((head) => {
    // Resolve the content element and the collapse wrapper. On first init
    // the .spoiler directly follows the head and gets wrapped; on re-init
    // after cleanup the wrapper already exists and is reused.
    let spoiler = head.nextElementSibling as HTMLElement | null;
    let collapse: HTMLElement;

    if (spoiler?.classList.contains("bb-collapse")) {
      collapse = spoiler;
      spoiler = collapse.querySelector<HTMLElement>(
        ":scope > .bb-collapse-clip > .spoiler",
      );
      if (!spoiler) return;
    } else if (spoiler?.classList.contains("spoiler")) {
      collapse = document.createElement("div");
      collapse.className = "bb-collapse";
      const clip = document.createElement("div");
      clip.className = "bb-collapse-clip";
      spoiler.parentNode?.insertBefore(collapse, spoiler);
      clip.appendChild(spoiler);
      collapse.appendChild(clip);
    } else {
      // Check BEFORE marking as initialized - if check fails, element can
      // be retried later
      if (import.meta.env.DEV) {
        console.warn(
          "[bbcodeInteractive] spoiler-head has no adjacent .spoiler element:",
          {
            head,
            nextSibling: head.nextElementSibling,
            nextSiblingClass: head.nextElementSibling?.className,
          },
        );
      }
      return;
    }

    // Mark as initialized AFTER successful check
    head.setAttribute("data-initialized", "true");

    // WCAG: Add accessibility attributes
    head.setAttribute("role", "button");
    head.setAttribute("tabindex", "0");
    head.setAttribute("aria-expanded", "false");

    // The head the server wrote carries the caption the author gave the
    // spoiler ([spoiler text="..."]), or the standard text when none was
    // given. Writing the standard text over it hid every caption from every
    // reader. It is read on the first initialisation and kept on the head: a
    // head initialised again after cleanup may be showing the text of the
    // open state.
    const caption =
      head.getAttribute("data-bb-caption") ??
      (head.textContent?.trim() || SPOILER_SHOW_TEXT);
    head.setAttribute("data-bb-caption", caption);

    // The words of the open head. The server writes them in data-swaptext: a
    // spoiler with a caption keeps it open as well, since the caption names
    // what is inside, and one without swaps to the standard words, as on DM2.
    // A head written without them (the spoiler an image is put behind) swaps
    // only when it shows the standard words.
    const openCaption =
      head.getAttribute("data-swaptext") ||
      (caption === SPOILER_SHOW_TEXT ? SPOILER_HIDE_TEXT : caption);

    // Initially collapsed, showing the caption
    collapse.classList.remove("open");
    collapse.inert = true;
    head.textContent = caption;

    // Toggle function. Pure class flip — the CSS grid transition animates
    // both directions and reverses cleanly mid-flight on rapid clicks.
    const toggleSpoiler = (): void => {
      const expand = !collapse.classList.contains("open");
      collapse.classList.toggle("open", expand);
      collapse.inert = !expand;
      head.textContent = expand ? openCaption : caption;
      head.setAttribute("aria-expanded", String(expand));
    };

    // Manual user toggle (click/keyboard): clears the registry's pending
    // bulk action; registry-driven calls use toggleSpoiler directly.
    const manualToggle = (): void => {
      notifyExpandableChanged();
      toggleSpoiler();
    };

    // Click handler
    const clickHandler = (e: Event): void => {
      e.preventDefault();
      manualToggle();
    };

    // Keyboard handler (WCAG)
    const keydownHandler = (e: Event): void => {
      handleKeyboardToggle(e as KeyboardEvent, manualToggle);
    };

    head.addEventListener("click", clickHandler);
    head.addEventListener("keydown", keydownHandler);

    // Register with the global expand/collapse-all registry so the
    // ScrollNav toggle button can drive every spoiler on the page at once.
    const unregister = registerExpandable({
      id: Symbol("spoiler"),
      isExpanded: () => collapse.classList.contains("open"),
      expand: () => {
        if (!collapse.classList.contains("open")) toggleSpoiler();
      },
      collapse: () => {
        if (collapse.classList.contains("open")) toggleSpoiler();
      },
    });

    // Store cleanup function
    cleanupFunctions.push(() => {
      head.removeEventListener("click", clickHandler);
      head.removeEventListener("keydown", keydownHandler);
      unregister();
    });
  });

  // Register all cleanup functions
  if (cleanupFunctions.length > 0) {
    if (import.meta.env.DEV) {
      console.debug(
        `[bbcodeInteractive] Initialized ${cleanupFunctions.length} spoilers`,
      );
    }
    registerCleanup(container, () => {
      cleanupFunctions.forEach((fn) => fn());
    });
  }
}

// ============================================================================
// NSFW INITIALIZATION
// ============================================================================

/**
 * Initialize NSFW toggle behavior with 18+ overlay for server-rendered NSFW blocks.
 * Server structure: <a class="nsfw-head">...</a><div class="nsfw-spoiler">...</div>
 *
 * The content is wrapped into the shared collapse structure (same smooth
 * grid-rows animation as spoilers) plus a positioning wrapper for the
 * 18+ overlay:
 *
 *   <a class="nsfw-head">
 *   <div class="bb-collapse [open]">
 *     <div class="bb-collapse-clip">
 *       <div class="nsfw-content-wrapper">   (position: relative)
 *         <div class="nsfw-spoiler">...</div>
 *         <div class="nsfw-overlay">18+</div>
 *
 * Features:
 * - 18+ overlay shown on first open only (confirmation persists)
 * - Clicking overlay fades it out (opacity, unified reveal curve)
 * - WCAG keyboard navigation
 */
export function initNsfw(container: HTMLElement | null): void {
  if (!container) return;

  const nsfwHeads = container.querySelectorAll(
    ".nsfw-head:not([data-initialized])",
  );
  const cleanupFunctions: Array<() => void> = [];

  nsfwHeads.forEach((head) => {
    // Resolve content, overlay and wrappers. On first init the
    // .nsfw-spoiler directly follows the head and gets wrapped; on re-init
    // after cleanup the structure already exists and is reused.
    const nsfwContent = head.nextElementSibling as HTMLElement | null;
    let collapse: HTMLElement;
    let overlay: HTMLElement;

    if (nsfwContent?.classList.contains("bb-collapse")) {
      collapse = nsfwContent;
      const wrapper = collapse.querySelector<HTMLElement>(
        ":scope > .bb-collapse-clip > .nsfw-content-wrapper",
      );
      const existingContent = wrapper?.querySelector<HTMLElement>(
        ":scope > .nsfw-spoiler",
      );
      const existingOverlay = wrapper?.querySelector<HTMLElement>(
        ":scope > .nsfw-overlay",
      );
      if (!existingContent || !existingOverlay) return;
      overlay = existingOverlay;
    } else if (nsfwContent?.classList.contains("nsfw-spoiler")) {
      // Wrapper for overlay positioning. No margin/border/padding here: the
      // outer spacing (5px, DM2 parity) lives on .nsfw-spoiler in
      // _BbcodeContent.sass and collapses through this borderless wrapper,
      // so the overlay's inset: 6px stays aligned with the yellow box.
      const wrapper = document.createElement("div");
      wrapper.className = "nsfw-content-wrapper";
      wrapper.style.position = "relative";

      collapse = document.createElement("div");
      collapse.className = "bb-collapse";
      const clip = document.createElement("div");
      clip.className = "bb-collapse-clip";

      nsfwContent.parentNode?.insertBefore(collapse, nsfwContent);
      collapse.appendChild(clip);
      clip.appendChild(wrapper);
      wrapper.appendChild(nsfwContent);

      // Create 18+ overlay
      overlay = document.createElement("div");
      overlay.className = "nsfw-overlay";
      overlay.innerHTML = `<span class="nsfw-warning">${NSFW_WARNING_TEXT}</span>`;
      overlay.setAttribute("role", "button");
      overlay.setAttribute("tabindex", "0");
      overlay.setAttribute(
        "aria-label",
        "Click to confirm you are 18+ and view content",
      );
      wrapper.appendChild(overlay);
    } else {
      // Check BEFORE marking as initialized - if check fails, element can
      // be retried later
      return;
    }

    // Mark as initialized AFTER successful check
    head.setAttribute("data-initialized", "true");

    // WCAG: Add accessibility attributes
    head.setAttribute("role", "button");
    head.setAttribute("tabindex", "0");
    head.setAttribute("aria-expanded", "false");
    head.textContent = NSFW_SHOW_TEXT;

    // Initially collapsed; inert keeps hidden content out of the tab order
    // and the accessibility tree (parity with the old display: none).
    collapse.classList.remove("open");
    collapse.inert = true;

    // Track if user has confirmed 18+ (persists for this block; recovered
    // from the overlay class when the structure is reused on re-init).
    let isConfirmed = overlay.classList.contains("confirmed");

    // Toggle function. Pure class flip — the CSS grid transition animates
    // both directions and reverses cleanly mid-flight on rapid clicks.
    const toggleNsfw = (): void => {
      const expand = !collapse.classList.contains("open");
      if (expand) {
        // Sync the overlay with the confirmation state before revealing:
        // unconfirmed blocks show the red zone again on every open.
        overlay.classList.toggle("confirmed", isConfirmed);
      }
      collapse.classList.toggle("open", expand);
      collapse.inert = !expand;
      head.textContent = expand ? NSFW_HIDE_TEXT : NSFW_SHOW_TEXT;
      head.setAttribute("aria-expanded", String(expand));
    };

    // Confirm overlay function. The overlay fades out via the CSS
    // opacity/backdrop-filter transition (unified reveal curve).
    const confirmOverlay = (): void => {
      isConfirmed = true;
      overlay.classList.add("confirmed");
    };

    // Event handlers
    const overlayClickHandler = (): void => confirmOverlay();
    const overlayKeydownHandler = (e: Event): void => {
      handleKeyboardToggle(e as KeyboardEvent, confirmOverlay);
    };

    // Manual user toggle (click/keyboard): clears the registry's pending
    // bulk action; registry-driven calls use toggleNsfw directly.
    const manualToggleNsfw = (): void => {
      notifyExpandableChanged();
      toggleNsfw();
    };

    const headClickHandler = (e: Event): void => {
      e.preventDefault();
      manualToggleNsfw();
    };
    const headKeydownHandler = (e: Event): void => {
      handleKeyboardToggle(e as KeyboardEvent, manualToggleNsfw);
    };

    overlay.addEventListener("click", overlayClickHandler);
    overlay.addEventListener("keydown", overlayKeydownHandler);
    head.addEventListener("click", headClickHandler);
    head.addEventListener("keydown", headKeydownHandler);

    // Register with the global expand/collapse-all registry. Expansion
    // also auto-confirms the 18+ overlay — the user has explicitly asked
    // to see everything via the ScrollNav toggle, so gating each block
    // behind a separate click would defeat the affordance.
    const unregister = registerExpandable({
      id: Symbol("nsfw"),
      isExpanded: () => collapse.classList.contains("open"),
      expand: () => {
        if (!collapse.classList.contains("open")) {
          isConfirmed = true;
          toggleNsfw();
        }
      },
      collapse: () => {
        if (collapse.classList.contains("open")) toggleNsfw();
      },
    });

    // Store cleanup function
    cleanupFunctions.push(() => {
      overlay.removeEventListener("click", overlayClickHandler);
      overlay.removeEventListener("keydown", overlayKeydownHandler);
      head.removeEventListener("click", headClickHandler);
      head.removeEventListener("keydown", headKeydownHandler);
      unregister();
    });
  });

  // Register all cleanup functions
  if (cleanupFunctions.length > 0) {
    registerCleanup(container, () => {
      cleanupFunctions.forEach((fn) => fn());
    });
  }
}

// ============================================================================
// PICTURES
// ============================================================================

/** Containers that already hear a click on their pictures. */
const imageListeners = new WeakSet<HTMLElement>();

/**
 * A click on a picture of the text opens it whole over the page (the viewer of
 * shared/ui/ImageViewer). In the text it is drawn no taller than the window
 * allows, and a tall map or sheet could not be read there at all.
 *
 * One listener on the container and not one per picture, so a picture that
 * arrives with new text later (an edit, a page of comments loaded in) opens
 * like the first ones did, and the container is initialised over and over
 * (ContentText re-runs this on every change of the text) without piling up.
 *
 * A picture inside [link] is the link's: the press goes where the author
 * sent it, as it did before. A press with a modifier is the browser's.
 */
function initImageViewer(container: HTMLElement): void {
  if (imageListeners.has(container)) return;

  const onClick = (event: MouseEvent) => {
    if (event.defaultPrevented || event.button !== 0) return;
    if (event.ctrlKey || event.metaKey || event.shiftKey || event.altKey) {
      return;
    }
    const target = event.target;
    if (!(target instanceof HTMLImageElement)) return;
    if (!target.matches(".bb-image") || !container.contains(target)) return;
    if (target.closest("a[href]")) return;

    event.preventDefault();
    openImageViewer(target.currentSrc || target.src);
  };

  container.addEventListener("click", onClick);
  imageListeners.add(container);
  registerCleanup(container, () => {
    container.removeEventListener("click", onClick);
    imageListeners.delete(container);
  });
}

// ============================================================================
// HIDDEN TEXT: WHO READS IT
// ============================================================================

/**
 * The hover waits longer on hidden text than on a control. A reader rests the
 * pointer on the text he reads, and a bubble after the usual 150 ms would stand
 * over the line above it, the line just read, every time the hand stopped.
 */
export const PRIVATE_HOVER_DELAY = 600;

/** The grace the bubble keeps while the pointer crosses between two lines. */
const PRIVATE_LEAVE_DELAY = 150;

/**
 * The words of the tooltip over a hidden block: "Получатели: DungeonMaster,
 * Assistant, Вася". The server writes the names on the block itself, in
 * data-recipients, the leads first (PrivateTag). They used to be a line of the
 * post after the block, " (Получатели: ...)", and the owner took it out (05.10:
 * "а зачем это вообще текстом, если при наведении показывается?"): the line
 * stretched the gaps of a justified paragraph around every hidden block.
 *
 * Read with getAttribute and shown as text: the names are the players' own.
 */
export function privateRecipientsText(piece: Element): string | null {
  const names = (piece.getAttribute("data-recipients") ?? "")
    .replace(/\s+/g, " ")
    .trim();
  return names ? `Получатели: ${names}` : null;
}

/**
 * The line of `element` nearest to the point, as an index into its client
 * rectangles (an inline box has one per line it spans): the one the point is
 * in, else the one nearest above or below it.
 */
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

/**
 * The point the bubble points at: on that line, where the pointer or the
 * finger is. A line of a block runs the width of the column, and a bubble
 * centred on the whole line put its arrow a column away from the word the
 * reader was on.
 */
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

/**
 * The one hidden block whose bubble is up (a page shows one bubble at a time),
 * how it was opened, and what closes it.
 */
let recipientsOpen: { piece: HTMLElement; byTap: boolean } | null = null;
let recipientsShowTimer: ReturnType<typeof setTimeout> | null = null;
let recipientsHideTimer: ReturnType<typeof setTimeout> | null = null;
let recipientsTapTimer: ReturnType<typeof setTimeout> | null = null;

function clearRecipientsTimers(): void {
  for (const timer of [
    recipientsShowTimer,
    recipientsHideTimer,
    recipientsTapTimer,
  ]) {
    if (timer) clearTimeout(timer);
  }
  recipientsShowTimer = null;
  recipientsHideTimer = null;
  recipientsTapTimer = null;
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
  byTap: boolean,
): void {
  clearRecipientsTimers();
  showAnchoredTooltip({
    text,
    owner: piece,
    anchor: lineAnchor(piece, lineIndexAt(piece, point.x, point.y), point.x),
  });
  recipientsOpen = { piece, byTap };
  listenWhileRecipientsOpen(true);
  if (byTap) {
    // One line of names goes by itself, a longer list waits for the finger.
    recipientsTapTimer = setTimeout(() => {
      recipientsTapTimer = null;
      const bubble = anchoredTooltipElement();
      if (
        !bubble ||
        closesByItself(bubble, bubble.querySelector(".tooltip-text"))
      ) {
        hideRecipients();
      }
    }, TAP_CLOSE_DELAY);
  }
}

// While the bubble is up, anything the reader does elsewhere takes it away: a
// press outside the block (the bubble itself included, it has nothing to
// press), a scroll, a key, a resize. A press inside the block is the block's
// own to decide: a mouse starting a selection there (see below), a tap that
// toggles.
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

/**
 * Whether a tap on the target inside a hidden block does something of its
 * own: follows a link, opens a spoiler (its head is a link), presses a
 * control, or opens a picture whole (initImageViewer). Text says nothing.
 */
function actsOnTap(target: EventTarget | null, piece: HTMLElement): boolean {
  if (tapTargetOf(target) !== "text") return true;
  return (
    target instanceof HTMLImageElement &&
    target.matches(".bb-image") &&
    piece.contains(target)
  );
}

/**
 * Who reads a hidden block, in a tooltip over the green text: the names the
 * server writes on the block (privateRecipientsText), over the line under the
 * pointer or the finger, so that on a long block it stands where the reader is.
 *
 * - Mouse: the bubble comes after PRIVATE_HOVER_DELAY over the line under the
 *   pointer and goes when the pointer leaves the block. A press on the block,
 *   or a drag into it with the button held, takes it away and keeps it away
 *   until the pointer leaves: that is a selection being made. A link, a
 *   spoiler and a picture inside answer the click at once, as anywhere.
 * - Touch, by the rule of the site's tooltips (touchRule.ts) with one
 *   addition of its own: the first tap anywhere on the hidden text opens the
 *   bubble over the line under the finger, a tap on a link, a spoiler or a
 *   picture inside included, and that tap does nothing else. While the bubble
 *   is open, a tap on the link follows it, on the spoiler opens it, on the
 *   picture opens it whole, and the bubble goes. A block that is a link and
 *   nothing more would otherwise never say on a phone whom it is for (the
 *   owner, 05.10: "как поступать, если приватный текст полностью состоит из
 *   ссылки или спойлера"). A second tap on the text, a tap elsewhere or
 *   TAP_CLOSE_DELAY (a one-line list only) close it. A scroll and a long
 *   press open nothing, the long press stays the phone's, for selecting and
 *   copying.
 * - Keyboard: nothing. The block takes no Tab stop: a room would get one on
 *   every green block.
 *
 * The bubble lives outside the text, so a selection of the post copies the
 * text alone.
 */
function initPrivateRecipients(container: HTMLElement): void {
  const pieces = container.querySelectorAll<HTMLElement>(".private-message");
  const cleanupFunctions: Array<() => void> = [];

  pieces.forEach((piece) => {
    if (privateListeners.has(piece)) return;
    const text = privateRecipientsText(piece);
    if (!text) return;

    trackInput();
    privateListeners.add(piece);

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
      if (isCompatMouseEvent()) return;
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
        () => showRecipients(piece, text, point, false),
        PRIVATE_HOVER_DELAY,
      );
    };

    const onMouseMove = (event: MouseEvent): void => {
      if (isCompatMouseEvent()) return;
      point = { x: event.clientX, y: event.clientY };
      if (event.buttons !== 0) keepAway();
    };

    const onMouseLeave = (): void => {
      if (isCompatMouseEvent()) return;
      if (recipientsOpen?.piece === piece) {
        if (recipientsOpen.byTap) return;
        if (recipientsHideTimer) clearTimeout(recipientsHideTimer);
        recipientsHideTimer = setTimeout(hideRecipients, PRIVATE_LEAVE_DELAY);
        return;
      }
      if (recipientsShowTimer) clearTimeout(recipientsShowTimer);
      recipientsShowTimer = null;
    };

    const onMouseDown = (): void => {
      if (isCompatMouseEvent()) return;
      keepAway();
    };

    // Heard in the capture phase, before the link, the spoiler head and the
    // picture under the finger hear the tap: the first tap is the bubble's.
    const onClick = (event: MouseEvent): void => {
      if (!isTap(event)) return;
      const acts = actsOnTap(event.target, piece);
      if (recipientsOpen?.piece === piece && recipientsOpen.byTap) {
        // The bubble has said its words: the link, the spoiler, the picture
        // do what they do now, and the text closes it as a second tap.
        hideRecipients();
        return;
      }
      if (acts) {
        event.preventDefault();
        event.stopPropagation();
      } else {
        // A tap into a selection is about the selection.
        const selection = window.getSelection();
        if (selection && !selection.isCollapsed) return;
      }
      showRecipients(piece, text, { x: event.clientX, y: event.clientY }, true);
    };

    piece.addEventListener("mouseenter", onMouseEnter);
    piece.addEventListener("mousemove", onMouseMove);
    piece.addEventListener("mouseleave", onMouseLeave);
    piece.addEventListener("mousedown", onMouseDown);
    piece.addEventListener("click", onClick, true);

    cleanupFunctions.push(() => {
      piece.removeEventListener("mouseenter", onMouseEnter);
      piece.removeEventListener("mousemove", onMouseMove);
      piece.removeEventListener("mouseleave", onMouseLeave);
      piece.removeEventListener("mousedown", onMouseDown);
      piece.removeEventListener("click", onClick, true);
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

// ============================================================================
// PUBLIC API
// ============================================================================

/**
 * Initialize all interactive BBCode elements in a container.
 *
 * @param container - The container element to initialize
 *
 * @example
 * ```typescript
 * // In Vue component
 * onMounted(() => {
 *   initBbcodeInteractive(containerRef.value);
 * });
 *
 * onBeforeUnmount(() => {
 *   cleanupBbcodeInteractive(containerRef.value);
 * });
 * ```
 */
export function initBbcodeInteractive(container: HTMLElement | null): void {
  if (import.meta.env.DEV) {
    if (!container) {
      console.warn(
        "[bbcodeInteractive] initBbcodeInteractive called with null container",
      );
    } else {
      const spoilerCount = container.querySelectorAll(".spoiler-head").length;
      const nsfwCount = container.querySelectorAll(".nsfw-head").length;
      const uninitializedSpoilers = container.querySelectorAll(
        ".spoiler-head:not([data-initialized])",
      ).length;
      const uninitializedNsfw = container.querySelectorAll(
        ".nsfw-head:not([data-initialized])",
      ).length;

      if (spoilerCount > 0 || nsfwCount > 0) {
        console.debug("[bbcodeInteractive] initBbcodeInteractive called", {
          spoilerHeads: spoilerCount,
          nsfwHeads: nsfwCount,
          uninitializedSpoilers,
          uninitializedNsfw,
          containerHTML: container.innerHTML.substring(0, 500),
        });
      }
    }
  }

  removeBrAroundBlockElements(container);
  initSpoilers(container);
  initNsfw(container);
  if (container) {
    initImageViewer(container);
    initPrivateRecipients(container);
  }
}
