import { getCurrentScope, onScopeDispose, readonly, ref } from "vue";

/**
 * How the shell stands at the width of the window, in one place for every
 * component that has to know. It asks one thing: whether the window is narrow.
 *
 * - Wider than the shell breakpoint: three columns, the side columns stand on
 *   the page, the header holds the menu and the statistics.
 * - Up to the shell breakpoint, narrow: one column, and the side columns, the
 *   menu and the statistics are in the one sliding column the button of the
 *   header brings in.
 *
 * A phone is narrow the same way: its page is laid out as wide as the
 * narrowest window and drawn smaller (index.html), so no width has a shell of
 * its own below the breakpoint.
 */

/**
 * The step of assets/styles/_Breakpoints.sass the shell switches on,
 * $bp-shell. A media query in a script cannot read a Sass variable, so the
 * number stands here a second time, and useShellMode.spec.ts reads the scale
 * and fails the day the two disagree.
 *
 * The step is inclusive, as every `max-width: $bp-shell` of the styles is: a
 * window 1000 wide is already narrow.
 */
export const SHELL_WIDTHS = { shell: 1000 } as const;

const NARROW_QUERY = `(max-width: ${SHELL_WIDTHS.shell}px)`;

/**
 * Whether the window is narrow, following it. Listens for as long as the scope
 * of the caller lives. Without matchMedia (a test that stubs none) the window
 * reads as wide.
 */
export function useShellMode() {
  const narrow = ref(false);
  if (typeof window === "undefined" || typeof window.matchMedia !== "function")
    return { narrow: readonly(narrow) };

  const list = window.matchMedia(NARROW_QUERY);
  const read = () => {
    narrow.value = list.matches;
  };
  read();
  list.addEventListener("change", read);
  if (getCurrentScope())
    onScopeDispose(() => list.removeEventListener("change", read));

  return {
    /** The side columns slide in from the button of the header. */
    narrow: readonly(narrow),
  };
}
