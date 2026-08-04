/**
 * "Reduce motion" is an operating-system setting, and the site answers it once,
 * globally, in CSS: Reset.sass collapses every transition and every animation
 * to 0.01ms under the media query, and eight components repeat the rule locally
 * for their own transitions.
 *
 * A JavaScript animation is outside that answer. `Element.animate()` is neither
 * a transition nor a CSS animation, no media query reaches it, and nothing in
 * the cascade can switch it off — so the code that starts one has to ask, and
 * this is the asking.
 *
 * Read at call time and not cached: the setting can change while the tab is
 * open, and a value read once at module load would answer for the whole
 * session.
 */
export function prefersReducedMotion(): boolean {
  return (
    typeof window !== "undefined" &&
    typeof window.matchMedia === "function" &&
    window.matchMedia("(prefers-reduced-motion: reduce)").matches
  );
}
