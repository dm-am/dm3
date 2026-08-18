/**
 * The spacing scale, in the one place JavaScript can read it.
 *
 * A popover is positioned by arithmetic, and arithmetic lives in a script while
 * every other distance on the site lives in `assets/styles/Variables.sass`. So
 * the numbers were written twice: the date field opened its calendar two pixels
 * below the input under a comment saying "matches $tiny offset of other
 * dropdowns", and the tooltip kept eight pixels off the viewport edge under a
 * name of its own while the date field kept the same eight under another. A
 * comment is not a link — the day `$grid-step` changes, the stylesheet moves and
 * these do not, and the calendar drifts away from every dropdown it was aligned
 * to.
 *
 * This module is that link, and geometry.spec.ts is what makes it one: it reads
 * Variables.sass and fails when the two scales disagree. Deriving the Sass from
 * here instead would need a build step to emit a partial; a check needs none and
 * catches the same drift.
 *
 * Pixels, unitless — these are operands, not CSS values.
 */

/** `$grid-step`. Every distance below is a whole or half multiple of it. */
export const GRID_STEP = 4;

/** `$tiny` — the gap a popover leaves between itself and its trigger. */
export const TINY = GRID_STEP / 2;

/** `$small` — the margin a floating layer keeps from the viewport edge. */
export const SMALL = 2 * GRID_STEP;

/**
 * Distance from a dropdown-shaped popover to the control that opened it. The
 * date picker and the filter dropdowns sit at the same offset, which is what
 * makes them read as one layer.
 */
export const POPOVER_GAP = TINY;

/**
 * How close to the edge of the viewport a floating layer may come before it is
 * pushed back in.
 */
export const VIEWPORT_EDGE = SMALL;

/**
 * Distance from a tooltip to its trigger. Larger than a dropdown's gap on
 * purpose: a tooltip points at the thing it describes and must not look
 * attached to it, and the arrow needs the room.
 */
export const TOOLTIP_OFFSET = 3 * GRID_STEP;
