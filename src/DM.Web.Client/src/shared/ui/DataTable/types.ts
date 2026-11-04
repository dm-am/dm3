/**
 * Column definition for DataTable
 */
export interface Column {
  /** Unique identifier for the column, used for slots and data access */
  key: string;
  /** Display label in header */
  label: string;
  /**
   * Column width (CSS value), where the column starts from in the table's
   * "auto" layout: a percentage is a share of the table, the cell's padding
   * included; px is the width of the content, the padding on top. The main
   * column of a table is left without one and takes the rest, and the rest
   * add up to less than the table, so every column gets the width it
   * declares where the table has the room.
   */
  width?: string;
  /** Text alignment */
  align?: "left" | "center" | "right";
}

/**
 * Sort state for DataTable
 */
export interface SortState {
  /** Column key being sorted */
  key: string;
  /** Sort direction */
  direction: "asc" | "desc";
}
