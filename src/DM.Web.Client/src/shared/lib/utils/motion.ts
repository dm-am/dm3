/**
 * "Reduce motion" for a JavaScript animation, which the global CSS answer in
 * Reset.sass does not reach. Read at call time: the setting can change while
 * the tab is open.
 */
export function prefersReducedMotion(): boolean {
  return (
    typeof window !== "undefined" &&
    typeof window.matchMedia === "function" &&
    window.matchMedia("(prefers-reduced-motion: reduce)").matches
  );
}
