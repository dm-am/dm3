import { SPOILER_SHOW_TEXT, SPOILER_HIDE_TEXT } from "./bbcodeConstants";
import { registerCleanup } from "./bbcodeCleanup";
import {
  bindCollapseHead,
  existingCollapse,
  wrapInCollapse,
} from "./bbcodeCollapse";

/**
 * The collapse wrapper of a spoiler head, built on first init and reused after
 * cleanup; null with no spoiler after the head.
 */
function resolveSpoilerCollapse(head: Element): HTMLElement | null {
  const existing = existingCollapse(head);
  if (existing) {
    const spoiler = existing.querySelector<HTMLElement>(
      ":scope > .bb-collapse-clip > .spoiler",
    );
    return spoiler ? existing : null;
  }

  const spoiler = head.nextElementSibling as HTMLElement | null;
  if (spoiler?.classList.contains("spoiler")) {
    return wrapInCollapse(spoiler, spoiler);
  }

  // Not marked initialized, so the head can be retried later.
  return null;
}

/**
 * Spoilers of the server's markup, on the shared collapse structure:
 *
 *   <a class="spoiler-head">
 *   <div class="bb-collapse [open]">
 *     <div class="bb-collapse-clip">
 *       <div class="spoiler">...</div>
 */
export function initSpoilers(container: HTMLElement | null): void {
  if (!container) return;

  const spoilerHeads = container.querySelectorAll(
    ".spoiler-head:not([data-initialized])",
  );
  const cleanupFunctions: Array<() => void> = [];

  spoilerHeads.forEach((head) => {
    const collapse = resolveSpoilerCollapse(head);
    if (!collapse) return;

    // The author's caption ([spoiler text="..."]) or the standard text, read
    // once and kept: a head initialised again may show the open text.
    const caption =
      head.getAttribute("data-bb-caption") ??
      (head.textContent?.trim() || SPOILER_SHOW_TEXT);

    // The open words come in data-swaptext; without it (the spoiler of an
    // image) the head swaps only from the standard words, as on DM2.
    const openCaption =
      head.getAttribute("data-swaptext") ||
      (caption === SPOILER_SHOW_TEXT ? SPOILER_HIDE_TEXT : caption);

    cleanupFunctions.push(
      bindCollapseHead({
        head,
        collapse,
        kind: "spoiler",
        headText: (open) => (open ? openCaption : caption),
      }),
    );
    head.setAttribute("data-bb-caption", caption);
  });

  if (cleanupFunctions.length > 0) {
    registerCleanup(container, () => {
      cleanupFunctions.forEach((fn) => fn());
    });
  }
}
