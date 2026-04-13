/**
 * BBCode Interactive Elements
 *
 * Shared utilities for initializing interactive BBCode elements (spoilers, NSFW)
 * in server-rendered content. Used by ChatView, ChatPage, and any other
 * component that displays BBCode content.
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
import { registerExpandable } from "@/shared/lib/composables/useExpandableRegistry";

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
 *
 * This is the preferred entry point. The lower-level DOM mutators
 * trimLeadingWhitespace/trimTrailingWhitespace remain exported for
 * advanced callers that already hold a live element reference, but
 * new code should reach for trimHtmlWhitespace instead.
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
 * Remove trailing empty elements (br, empty p/div, whitespace-only text nodes)
 * from the end of a container. This prevents empty space at the bottom of
 * truncated content.
 */
export function trimTrailingWhitespace(container: HTMLElement | null): void {
  if (!container) return;

  while (container.lastChild) {
    const last = container.lastChild;

    // Text node with only whitespace - remove
    if (last.nodeType === Node.TEXT_NODE) {
      if ((last.textContent || "").trim() === "") {
        last.remove();
        continue;
      }
      // Non-empty text, trim trailing whitespace and stop
      last.textContent = (last.textContent || "").trimEnd();
      break;
    }

    // Element node
    if (last.nodeType === Node.ELEMENT_NODE) {
      const elem = last as Element;

      if (isTrimmableEmptyElement(elem)) {
        elem.remove();
        continue;
      }

      // Non-empty element - recursively trim its contents, then stop
      trimTrailingWhitespace(elem as HTMLElement);
      break;
    }

    // Other node types - stop
    break;
  }
}

/**
 * Mirror of trimTrailingWhitespace for the start of a container. Prevents
 * phantom leading empty lines (author-inserted blank lines at the top of a
 * post or topic) from eating into the truncation budget — without them,
 * scrollHeight no longer over-reports and the collapsed view shows real
 * content from the first line.
 */
export function trimLeadingWhitespace(container: HTMLElement | null): void {
  if (!container) return;

  while (container.firstChild) {
    const first = container.firstChild;

    // Text node with only whitespace - remove
    if (first.nodeType === Node.TEXT_NODE) {
      if ((first.textContent || "").trim() === "") {
        first.remove();
        continue;
      }
      // Non-empty text, trim leading whitespace and stop
      first.textContent = (first.textContent || "").trimStart();
      break;
    }

    // Element node
    if (first.nodeType === Node.ELEMENT_NODE) {
      const elem = first as Element;

      if (isTrimmableEmptyElement(elem)) {
        elem.remove();
        continue;
      }

      // Non-empty element - recursively trim its contents, then stop
      trimLeadingWhitespace(elem as HTMLElement);
      break;
    }

    // Other node types - stop
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
 * Initialize spoiler toggle behavior for server-rendered spoilers
 * Structure: <a class="spoiler-head">...</a><div class="spoiler">...</div>
 *
 * WCAG features:
 * - role="button" for screen readers
 * - tabindex="0" for keyboard focus
 * - aria-expanded to indicate state
 * - Enter/Space keyboard support
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
    const spoiler = head.nextElementSibling as HTMLElement;
    // Check BEFORE marking as initialized - if check fails, element can be retried later
    if (!spoiler?.classList.contains("spoiler")) {
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

    // Initially hide spoilers with standard text
    spoiler.classList.add("hidden");
    head.textContent = SPOILER_SHOW_TEXT;

    // Toggle function
    const toggleSpoiler = (): void => {
      const isHidden = spoiler.classList.contains("hidden");

      if (isHidden) {
        spoiler.classList.remove("hidden");
        head.textContent = SPOILER_HIDE_TEXT;
        head.setAttribute("aria-expanded", "true");
      } else {
        spoiler.classList.add("hidden");
        head.textContent = SPOILER_SHOW_TEXT;
        head.setAttribute("aria-expanded", "false");
      }
    };

    // Click handler
    const clickHandler = (e: Event): void => {
      e.preventDefault();
      toggleSpoiler();
    };

    // Keyboard handler (WCAG)
    const keydownHandler = (e: Event): void => {
      handleKeyboardToggle(e as KeyboardEvent, toggleSpoiler);
    };

    head.addEventListener("click", clickHandler);
    head.addEventListener("keydown", keydownHandler);

    // Register with the global expand/collapse-all registry so the
    // ScrollNav toggle button can drive every spoiler on the page at once.
    const unregister = registerExpandable({
      id: Symbol("spoiler"),
      isExpanded: () => !spoiler.classList.contains("hidden"),
      expand: () => {
        if (spoiler.classList.contains("hidden")) toggleSpoiler();
      },
      collapse: () => {
        if (!spoiler.classList.contains("hidden")) toggleSpoiler();
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
 * Initialize NSFW toggle behavior with 18+ overlay for server-rendered NSFW blocks
 * Structure: <a class="nsfw-head">...</a><div class="nsfw-spoiler">...</div>
 *
 * Features:
 * - 18+ overlay shown on first open only (confirmation persists)
 * - Clicking overlay removes it permanently for that block
 * - WCAG keyboard navigation
 */
export function initNsfw(container: HTMLElement | null): void {
  if (!container) return;

  const nsfwHeads = container.querySelectorAll(
    ".nsfw-head:not([data-initialized])",
  );
  const cleanupFunctions: Array<() => void> = [];

  nsfwHeads.forEach((head) => {
    const nsfwContent = head.nextElementSibling;
    // Check BEFORE marking as initialized - if check fails, element can be retried later
    if (!nsfwContent?.classList.contains("nsfw-spoiler")) return;

    // Mark as initialized AFTER successful check
    head.setAttribute("data-initialized", "true");

    // WCAG: Add accessibility attributes
    head.setAttribute("role", "button");
    head.setAttribute("tabindex", "0");
    head.setAttribute("aria-expanded", "false");
    head.textContent = NSFW_SHOW_TEXT;

    // Create wrapper for overlay positioning
    const wrapper = document.createElement("div");
    wrapper.className = "nsfw-content-wrapper";
    wrapper.style.cssText = "position: relative; margin: 4px 0; display: none;";

    // Move content into wrapper
    nsfwContent.parentNode?.insertBefore(wrapper, nsfwContent);
    wrapper.appendChild(nsfwContent);

    // Create 18+ overlay
    const overlay = document.createElement("div");
    overlay.className = "nsfw-overlay";
    overlay.innerHTML = `<span class="nsfw-warning">${NSFW_WARNING_TEXT}</span>`;
    overlay.setAttribute("role", "button");
    overlay.setAttribute("tabindex", "0");
    overlay.setAttribute(
      "aria-label",
      "Click to confirm you are 18+ and view content",
    );
    wrapper.appendChild(overlay);

    // Track if user has confirmed 18+ (persists for this block)
    let isConfirmed = false;

    // Toggle function
    const toggleNsfw = (): void => {
      const isHidden = wrapper.style.display === "none";

      if (isHidden) {
        wrapper.style.display = "block";
        overlay.style.display = isConfirmed ? "none" : "flex";
        head.textContent = NSFW_HIDE_TEXT;
        head.setAttribute("aria-expanded", "true");
      } else {
        wrapper.style.display = "none";
        head.textContent = NSFW_SHOW_TEXT;
        head.setAttribute("aria-expanded", "false");
      }
    };

    // Confirm overlay function
    const confirmOverlay = (): void => {
      isConfirmed = true;
      overlay.style.display = "none";
    };

    // Event handlers
    const overlayClickHandler = (): void => confirmOverlay();
    const overlayKeydownHandler = (e: Event): void => {
      handleKeyboardToggle(e as KeyboardEvent, confirmOverlay);
    };

    const headClickHandler = (e: Event): void => {
      e.preventDefault();
      toggleNsfw();
    };
    const headKeydownHandler = (e: Event): void => {
      handleKeyboardToggle(e as KeyboardEvent, toggleNsfw);
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
      isExpanded: () => wrapper.style.display !== "none",
      expand: () => {
        if (wrapper.style.display === "none") {
          isConfirmed = true;
          toggleNsfw();
        }
      },
      collapse: () => {
        if (wrapper.style.display !== "none") toggleNsfw();
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
