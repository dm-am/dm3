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
