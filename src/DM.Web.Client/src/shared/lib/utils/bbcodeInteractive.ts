/**
 * BBCode Interactive Elements
 *
 * Shared utilities for initializing interactive BBCode elements (spoilers, NSFW)
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

    // Initially collapsed with standard text
    collapse.classList.remove("open");
    collapse.inert = true;
    head.textContent = SPOILER_SHOW_TEXT;

    // Toggle function. Pure class flip — the CSS grid transition animates
    // both directions and reverses cleanly mid-flight on rapid clicks.
    const toggleSpoiler = (): void => {
      const expand = !collapse.classList.contains("open");
      collapse.classList.toggle("open", expand);
      collapse.inert = !expand;
      head.textContent = expand ? SPOILER_HIDE_TEXT : SPOILER_SHOW_TEXT;
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
}
