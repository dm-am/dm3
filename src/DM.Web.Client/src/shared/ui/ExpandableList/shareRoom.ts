/**
 * A column of a list that has no width of its own, as the list measured it.
 * Both numbers are the cell's width with its own left padding.
 */
export interface FlexibleColumn {
  /** Its longest word; for a column that gives way, its longest line. */
  want: number;
  /** The least it keeps: its longest word, or, for a column that gives way, its label. */
  hold: number;
}

/**
 * The widths of the flexible columns of a row, given the room the columns with
 * a width in px leave them.
 *
 * Every column gets its longest word, and the room over that is shared
 * equally. A table laid out "fixed" shares the whole room equally instead, and
 * that cut a word in two whenever the columns' words were unequal: the staff
 * table on a phone gave "Администраторы" and the logins the same width and
 * broke the role in the middle.
 *
 * When the words do not all fit, a column that gives way (its lines wrap,
 * and a word wider than what is left breaks by the rule of the site) gives up
 * its room first, the columns that do evenly, none below what it holds. If even that is not enough, every column stands at what it holds and
 * the list is wider than its box.
 */
export function shareRoom(
  room: number,
  columns: readonly FlexibleColumn[],
): number[] {
  const widths = columns.map((column) => column.want);
  let over = widths.reduce((sum, width) => sum + width, 0) - room;
  if (over <= 0) {
    const extra = -over / columns.length;
    return widths.map((width) => width + extra);
  }
  // Each pass either settles the overflow or brings a column down to what it
  // holds, so the passes are at most as many as the columns. Half a pixel is
  // settled: the fractions of a division would otherwise never reach zero.
  for (;;) {
    const giving = widths
      .map((width, index) => (width > columns[index]!.hold ? index : -1))
      .filter((index) => index >= 0);
    if (giving.length === 0 || over < 0.5) return widths;
    const each = over / giving.length;
    for (const index of giving) {
      const take = Math.min(each, widths[index]! - columns[index]!.hold);
      widths[index]! -= take;
      over -= take;
    }
  }
}
