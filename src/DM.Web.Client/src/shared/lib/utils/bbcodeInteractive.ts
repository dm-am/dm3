/**
 * Brings the server-rendered BBCode of a container to life: spoilers, NSFW,
 * pictures that open whole and the tooltip naming who reads private text.
 * cleanupBbcodeInteractive (bbcodeCleanup.ts) takes the listeners off.
 */

import { initSpoilers } from "./bbcodeSpoiler";
import { initNsfw } from "./bbcodeNsfw";
import { initImageViewer } from "./bbcodeImageViewer";
import { initPrivateRecipients } from "./bbcodePrivateRecipients";

/**
 * Removes the <br> next to block elements (spoiler and nsfw heads and bodies),
 * which break the line themselves.
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
  removeBrAroundBlockElements(container);
  initSpoilers(container);
  initNsfw(container);
  if (container) {
    initImageViewer(container);
    initPrivateRecipients(container);
  }
}
