import {
  NSFW_SHOW_TEXT,
  NSFW_HIDE_TEXT,
  NSFW_WARNING_TEXT,
} from "./bbcodeConstants";
import { registerCleanup } from "./bbcodeCleanup";
import {
  bindCollapseHead,
  existingCollapse,
  handleKeyboardToggle,
  wrapInCollapse,
} from "./bbcodeCollapse";

/** The 18+ overlay over the content until the reader confirms. */
function createNsfwOverlay(): HTMLElement {
  const overlay = document.createElement("div");
  overlay.className = "nsfw-overlay";
  overlay.innerHTML = `<span class="nsfw-warning">${NSFW_WARNING_TEXT}</span>`;
  overlay.setAttribute("role", "button");
  overlay.setAttribute("tabindex", "0");
  overlay.setAttribute(
    "aria-label",
    "Подтвердить возраст 18+ и показать содержимое",
  );
  return overlay;
}

/**
 * The collapse wrapper of an nsfw head and its overlay, built on first init and
 * reused after cleanup; null with no nsfw content after the head.
 */
function resolveNsfwCollapse(
  head: Element,
): { collapse: HTMLElement; overlay: HTMLElement } | null {
  const existing = existingCollapse(head);
  if (existing) {
    const wrapper = existing.querySelector<HTMLElement>(
      ":scope > .bb-collapse-clip > .nsfw-content-wrapper",
    );
    const existingContent = wrapper?.querySelector<HTMLElement>(
      ":scope > .nsfw-spoiler",
    );
    const existingOverlay = wrapper?.querySelector<HTMLElement>(
      ":scope > .nsfw-overlay",
    );
    if (!existingContent || !existingOverlay) return null;
    return { collapse: existing, overlay: existingOverlay };
  }

  const nsfwContent = head.nextElementSibling as HTMLElement | null;
  // Check BEFORE marking as initialized - if check fails, element can
  // be retried later
  if (!nsfwContent?.classList.contains("nsfw-spoiler")) return null;

  // No spacing here: the outer 5px (DM2) on .nsfw-spoiler collapses through
  // this wrapper, keeping the overlay's 6px inset on the yellow box.
  const wrapper = document.createElement("div");
  wrapper.className = "nsfw-content-wrapper";
  wrapper.style.position = "relative";

  const collapse = wrapInCollapse(nsfwContent, wrapper);
  wrapper.appendChild(nsfwContent);

  const overlay = createNsfwOverlay();
  wrapper.appendChild(overlay);
  return { collapse, overlay };
}

/**
 * NSFW blocks with the 18+ overlay of their first opening, on the shared
 * collapse structure:
 *
 *   <a class="nsfw-head">
 *   <div class="bb-collapse [open]">
 *     <div class="bb-collapse-clip">
 *       <div class="nsfw-content-wrapper">   (position: relative)
 *         <div class="nsfw-spoiler">...</div>
 *         <div class="nsfw-overlay">18+</div>
 */
export function initNsfw(container: HTMLElement | null): void {
  if (!container) return;

  const nsfwHeads = container.querySelectorAll(
    ".nsfw-head:not([data-initialized])",
  );
  const cleanupFunctions: Array<() => void> = [];

  nsfwHeads.forEach((head) => {
    const resolved = resolveNsfwCollapse(head);
    if (!resolved) return;
    const { collapse, overlay } = resolved;

    // Track if user has confirmed 18+ (persists for this block; recovered
    // from the overlay class when the structure is reused on re-init).
    let isConfirmed = overlay.classList.contains("confirmed");

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

    overlay.addEventListener("click", overlayClickHandler);
    overlay.addEventListener("keydown", overlayKeydownHandler);

    const unbind = bindCollapseHead({
      head,
      collapse,
      kind: "nsfw",
      headText: (open) => (open ? NSFW_HIDE_TEXT : NSFW_SHOW_TEXT),
      // Sync the overlay with the confirmation state before revealing:
      // unconfirmed blocks show the red zone again on every open.
      beforeOpen: () => overlay.classList.toggle("confirmed", isConfirmed),
      // "Развернуть все" confirms the overlay: the reader asked to see everything.
      beforeBulkOpen: () => {
        isConfirmed = true;
      },
    });

    // Store cleanup function
    cleanupFunctions.push(() => {
      overlay.removeEventListener("click", overlayClickHandler);
      overlay.removeEventListener("keydown", overlayKeydownHandler);
      unbind();
    });
  });

  // Register all cleanup functions
  if (cleanupFunctions.length > 0) {
    registerCleanup(container, () => {
      cleanupFunctions.forEach((fn) => fn());
    });
  }
}
