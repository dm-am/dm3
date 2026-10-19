/**
 * The collapsible block of the markup, wrapped so both directions animate
 * (.bb-collapse in _BbcodeSpoilerNsfw.sass):
 *
 *   <a class="...-head">
 *   <div class="bb-collapse [open]">
 *     <div class="bb-collapse-clip">
 *       ...the content
 *
 * The head is a role="button" with tabindex, aria-expanded and Enter/Space;
 * the collapsed wrapper is inert, out of the tab order and the tree.
 */

import {
  registerExpandable,
  notifyExpandableChanged,
} from "@/shared/lib/composables/useExpandableRegistry";

/**
 * Handle keyboard events for toggle elements (WCAG 2.1 compliance)
 */
export function handleKeyboardToggle(
  e: KeyboardEvent,
  clickHandler: () => void,
): void {
  if (e.key === "Enter" || e.key === " ") {
    e.preventDefault();
    clickHandler();
  }
}

/** The wrapper a head already got on an earlier init, or null. */
export function existingCollapse(head: Element): HTMLElement | null {
  const next = head.nextElementSibling as HTMLElement | null;
  return next?.classList.contains("bb-collapse") ? next : null;
}

/**
 * Puts .bb-collapse > .bb-collapse-clip where `anchor` stood, `inner` in the
 * clip.
 */
export function wrapInCollapse(
  anchor: HTMLElement,
  inner: HTMLElement,
): HTMLElement {
  const collapse = document.createElement("div");
  collapse.className = "bb-collapse";
  const clip = document.createElement("div");
  clip.className = "bb-collapse-clip";
  anchor.parentNode?.insertBefore(collapse, anchor);
  clip.appendChild(inner);
  collapse.appendChild(clip);
  return collapse;
}

export interface CollapseHead {
  head: Element;
  collapse: HTMLElement;
  /** Description of the registry symbol: "spoiler", "nsfw". */
  kind: string;
  /** The words of the head, closed and open. */
  headText: (open: boolean) => string;
  /** Runs before the content opens (the nsfw block syncs its overlay). */
  beforeOpen?: () => void;
  /**
   * Runs before "Развернуть все" opens it (the nsfw block counts it as
   * confirmed).
   */
  beforeBulkOpen?: () => void;
}

/**
 * Marks the head initialised, closes the block, hears click and keys, joins
 * the registry. Returns the cleanup.
 */
export function bindCollapseHead(block: CollapseHead): () => void {
  const { head, collapse } = block;

  // Mark as initialized AFTER successful check
  head.setAttribute("data-initialized", "true");

  // WCAG: Add accessibility attributes
  head.setAttribute("role", "button");
  head.setAttribute("tabindex", "0");
  head.setAttribute("aria-expanded", "false");

  // Initially collapsed, showing the words of the closed head
  head.textContent = block.headText(false);
  collapse.classList.remove("open");
  collapse.inert = true;

  // Toggle function. Pure class flip — the CSS grid transition animates
  // both directions and reverses cleanly mid-flight on rapid clicks.
  const toggle = (): void => {
    const expand = !collapse.classList.contains("open");
    if (expand) block.beforeOpen?.();
    collapse.classList.toggle("open", expand);
    collapse.inert = !expand;
    head.textContent = block.headText(expand);
    head.setAttribute("aria-expanded", String(expand));
  };

  // Manual user toggle (click/keyboard): clears the registry's pending
  // bulk action; registry-driven calls use toggle directly.
  const manualToggle = (): void => {
    notifyExpandableChanged();
    toggle();
  };

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

  // Register with the global expand/collapse-all registry so the ScrollNav
  // toggle button can drive every block on the page at once. Last: a block
  // that comes after "Развернуть все" is opened right here.
  const unregister = registerExpandable({
    id: Symbol(block.kind),
    isExpanded: () => collapse.classList.contains("open"),
    expand: () => {
      if (!collapse.classList.contains("open")) {
        block.beforeBulkOpen?.();
        toggle();
      }
    },
    collapse: () => {
      if (collapse.classList.contains("open")) toggle();
    },
  });

  return () => {
    head.removeEventListener("click", clickHandler);
    head.removeEventListener("keydown", keydownHandler);
    unregister();
  };
}
