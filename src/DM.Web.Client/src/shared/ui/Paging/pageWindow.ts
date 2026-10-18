/**
 * The page numbers a strip shows around the current page.
 *
 * Ten where the line holds them, fewer where it does not, and the dots and the
 * two jumps at every width: "<< ... 4 5 6 ... >>" (owner, 5 October 2026: if
 * it does not fit, show fewer numbers, and keep the dots). The strip is one
 * line at any width, a three-digit page at 360 included. Paging.vue measures
 * the line and the labels; this file decides from the measure.
 */
export const PAGES_WIDE = 10;

/** The fewest numbers a strip shows: the current page and one to each side. */
export const PAGES_FEWEST = 3;

/** What a strip has to lay out, in CSS pixels. */
export interface StripMeasure {
  /** The width of its line. */
  room: number;
  /** One number at the widest label of the strip, its sides and margins included. */
  number: number;
  /** What the bold of the current page adds to its label. */
  current: number;
  /** "<<", two "..." and ">>" together, their sides and margins included. */
  jumps: number;
}

/**
 * How many numbers fit in the line. A strip of ten pages or fewer that fits
 * whole shows every page and needs no jumps; any other gets the most numbers
 * that leave room for all four jumps, so its window does not change size as
 * the reader steps from the first page to the middle.
 */
export function pagesThatFit(pages: number, measure: StripMeasure): number {
  const { room, number, current, jumps } = measure;
  if (pages <= PAGES_WIDE && pages * number + current <= room) return pages;
  const fit = Math.floor((room - current - jumps) / number);
  return Math.min(PAGES_WIDE, Math.max(PAGES_FEWEST, fit));
}

export interface PageWindow {
  lowerBound: number;
  upperBound: number;
}

/**
 * The first and the last number of the window. Near the start it starts at 1,
 * near the end it ends at the last page, and between them the current page
 * stands in its middle (the left middle of an even window, as it always did).
 */
export function pageWindow(
  current: number,
  total: number,
  size: number,
): PageWindow {
  if (total <= size) return { lowerBound: 1, upperBound: total };
  const before = Math.ceil(size / 2) - 1;
  const after = size - before - 1;
  if (current <= before + 1) return { lowerBound: 1, upperBound: size };
  if (current > total - after)
    return { lowerBound: total - size + 1, upperBound: total };
  return { lowerBound: current - before, upperBound: current + after };
}
