/**
 * The spacing scale of Variables.sass, for the popovers positioned by
 * arithmetic; geometry.spec.ts fails when the two disagree. Unitless pixels.
 */

/** `$grid-step`. Every distance below is a whole or half multiple of it. */
export const GRID_STEP = 4;

/** `$tiny` — the gap a popover leaves between itself and its trigger. */
export const TINY = GRID_STEP / 2;

/** `$small` — the margin a floating layer keeps from the viewport edge. */
export const SMALL = 2 * GRID_STEP;

/** `$medium` — how far the toolbar of a chat message rises above it. */
export const MEDIUM = 4 * GRID_STEP;

/** From a dropdown-shaped popover to its control: date picker and filters alike. */
export const POPOVER_GAP = TINY;

/** How close to the viewport edge a floating layer comes before it is pushed back. */
export const VIEWPORT_EDGE = SMALL;

/**
 * From a tooltip to its trigger, larger than a dropdown's: it must not look
 * attached, and the arrow needs the room.
 */
export const TOOLTIP_OFFSET = 3 * GRID_STEP;
