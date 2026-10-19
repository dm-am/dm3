export interface DropdownItemProps {
  label: string;
  hint?: string;
  avatarUrl?: string;
  highlighted?: boolean;
  /** Whether to indent this item (for hierarchy) */
  indent?: boolean;
  /** Whether this item has sub-options (shows arrow) */
  hasSubOptions?: boolean;
  searchQuery?: string;
}

export interface DateRangePickerProps {
  /** From date value (YYYY-MM-DD format) */
  fromValue: string | null;
  /** To date value (YYYY-MM-DD format) */
  toValue: string | null;
  fromLabel?: string;
  toLabel?: string;
  showClearButton?: boolean;
  /**
   * Days after today can be picked. Off everywhere but the dates of a poll,
   * which starts and ends ahead: nothing else a filter finds happens tomorrow.
   */
  allowFuture?: boolean;
}

export interface NumericRangePickerProps {
  /** Minimum value (null = unset) */
  minValue: number | null;
  /** Maximum value (null = unset) */
  maxValue: number | null;
  allowNegative?: boolean;
}

export interface BubbleValue {
  id: string;
  label: string;
}

export interface ExpandableBubbleProps {
  /** Prefix text (e.g., "Авторы:") */
  prefix: string;
  values: BubbleValue[];
  /** Maximum visible values before collapse */
  maxVisible?: number;
}

export interface SortOption {
  /** Value used in URL/API */
  value: string;
  label: string;
  hint?: string;
  defaultDirection?: "asc" | "desc";
}

export interface SortButtonProps {
  options: readonly SortOption[];
  sortBy: string;
  sortOrder: "asc" | "desc";
}

export interface ListOption {
  value: string;
  label: string;
  hint?: string;
  hasSubOptions?: boolean;
}

export interface OptionsListProps {
  options: ListOption[];
  highlightedIndex?: number;
  filterQuery?: string;
}
