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
  /** Whether column is sortable */
  sortable?: boolean;
  /** Sort direction applied when first sorting by this column (default "asc") */
  defaultDirection?: "asc" | "desc";
  /**
   * Place in the order of need, at any width (DataTable): where the table
   * does not fit its box, whole columns go from the largest rank down, one by
   * one, until it fits. 1 is the main column of the table (a title, a name, a
   * file), never hidden. A column without a rank is never hidden either: a
   * column of an action ("Удалить", "Восстановить") is declared without one,
   * and so is every column of a table nobody ranked, which then scrolls in its
   * box where it does not fit. The column the table is sorted by stands
   * whatever its rank.
   */
  rank?: number;
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
