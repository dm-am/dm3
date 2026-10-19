// The page scrolls inside App.vue's ".main", not the window: every caller
// takes the element from here rather than querying a class name.

let scrollContainer: HTMLElement | null = null;

export function setScrollContainer(el: HTMLElement | null): void {
  scrollContainer = el;
}

/** The registered container, or null before App.vue has mounted. */
export function getScrollContainer(): HTMLElement | null {
  return scrollContainer;
}

/** Scrolls the registered container to top; no-op when unset. */
export function scrollContentToTop(behavior: ScrollBehavior = "auto"): void {
  scrollContainer?.scrollTo({ top: 0, behavior });
}

/**
 * Brings a block's top into view, offsetPx below the container's top, when it
 * is out of view; a visible top keeps the reading position.
 */
export function scrollBlockIntoView(el: HTMLElement, offsetPx = 16): void {
  if (!scrollContainer) return;
  const containerRect = scrollContainer.getBoundingClientRect();
  const blockTop = el.getBoundingClientRect().top;
  const topVisible =
    blockTop >= containerRect.top && blockTop <= containerRect.bottom;
  if (topVisible) return;
  scrollContainer.scrollTo({
    top: scrollContainer.scrollTop + (blockTop - containerRect.top) - offsetPx,
  });
}

/** What the reader does to take the scroll over from a held anchor. */
const READER_INPUT = ["wheel", "touchstart", "keydown", "pointerdown"] as const;

/**
 * Keeps an anchor's top at the top of the content while the blocks around it
 * change height (a skeleton giving way to what it waited for), until the reader
 * scrolls, presses or types. The router scrolls to an anchor once, a frame
 * after the page is drawn, and the blocks above it settle later.
 *
 * @param target The element the address names
 * @param settling The element whose size moves the target: the page around it
 * @returns The release, for the target's unmount
 */
export function holdAnchor(
  target: HTMLElement,
  settling: HTMLElement,
): () => void {
  if (typeof ResizeObserver === "undefined") return () => {};
  const observer = new ResizeObserver(() =>
    target.scrollIntoView({ block: "start" }),
  );
  const release = () => {
    observer.disconnect();
    for (const type of READER_INPUT)
      window.removeEventListener(type, release, true);
  };
  observer.observe(settling);
  for (const type of READER_INPUT)
    window.addEventListener(type, release, { capture: true, passive: true });
  return release;
}
