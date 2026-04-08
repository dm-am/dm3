/**
 * Column definition for DataTable
 */
export interface Column {
  /** Unique identifier for the column, used for slots and data access */
  key: string;
  /** Display label in header */
  label: string;
  /** Column width (CSS value) */
  width?: string;
  /** Text alignment */
  align?: "left" | "center" | "right";
  /** Whether column is sortable */
  sortable?: boolean;
  /** Whether to hide on mobile screens */
  hideOnMobile?: boolean;
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
