// Scroll container registry — the app content scrolls inside App.vue's
// ".main" element (not the window), so window.scrollTo doesn't work.
// App.vue registers the element on mount; paging and the router call
// scrollContentToTop() to reset scroll position on navigation.

let scrollContainer: HTMLElement | null = null;

export function setScrollContainer(el: HTMLElement | null): void {
  scrollContainer = el;
}

/** Scrolls the registered container to top; no-op when unset. */
export function scrollContentToTop(behavior: ScrollBehavior = "auto"): void {
  scrollContainer?.scrollTo({ top: 0, behavior });
}

/**
 * Brings a block's TOP edge into view inside the registered container.
 *
 * Pagination helper: when the user pages a list from a control at its
 * bottom, the interesting content (the top of the repaginated block) is
 * usually scrolled out of view. If the block's top is NOT visible (above
 * the container's viewport or below its bottom edge), the container is
 * scrolled so the top lands `offsetPx` below the container's top edge.
 * If the top is already visible, nothing happens — the reading position
 * is left alone. No-op when no container is registered.
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
