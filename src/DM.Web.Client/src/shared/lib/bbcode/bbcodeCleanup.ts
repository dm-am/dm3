/**
 * Every part that brings the server's markup to life registers how to take its
 * listeners off, and one call takes them all off.
 */

const cleanupMap = new WeakMap<HTMLElement, () => void>();

/**
 * Store a cleanup function for a container
 */
export function registerCleanup(
  container: HTMLElement,
  cleanup: () => void,
): void {
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

/** Call in onBeforeUnmount or when the content changes. */
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
